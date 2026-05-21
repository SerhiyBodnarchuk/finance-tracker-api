# Phase 0 — Research: Domain Entities and API DTOs

**Feature**: `001-domain-entities-dtos` · **Plan**: [plan.md](plan.md) · **Spec**: [spec.md](spec.md)

This document resolves the five design questions left open by the spec's "shape only, technology-agnostic" framing — four about the data model, one about the test framework setup the user scaffolded. None are flagged `NEEDS CLARIFICATION` in [plan.md](plan.md) — they're settled here so the data model and tests can be written without revisiting them.

---

## 1. How to represent the polymorphic `ReportRequest.Data` payload in C#

**Decision**: `ReportRequest` is a record `(ReportType Type, JsonElement Data)`. Each report strategy deserializes the `JsonElement` into its own typed payload (`PeriodReportData` in this feature; `IsoWeekReportData` and friends in later features) inside its `Generate(...)` method, using `JsonSerializer.Deserialize<T>(Data)` with the shared `JsonSerializerOptions`.

> **MVP scope (2026-05-21)**: only `PeriodReportData` is defined in this feature. `ReportType.IsoWeek` is retained in the enum as a documented future value so the US5 test fixture (the README's `ReportResult` example, which uses an ISO-week descriptor) can serve as the canonical wire-shape reference. The `IsoWeekReportData` DTO is added when the IsoWeek strategy ships; the JsonElement-based envelope means *no change* to `ReportRequest` itself when that happens.

**Rationale**:
- The user's spec (FR-011) mandates a single envelope shape that accepts *any* per-type payload, with no envelope-level coupling to the per-type DTOs. `JsonElement` is precisely that: a structured value that has been parsed but not yet bound.
- This matches the existing design captured in `ai-artifacts/Specifications/period-report-strategy-spec.md` (`record ReportRequest(string Type, JsonElement Parameters)`) — same data flow, just `string Type` → `ReportType Type` and `Parameters` → `Data`.
- It keeps the envelope record honest: `JsonElement` is itself immutable and round-trips through serialization losslessly, so the spec's FR-026 round-trip requirement holds without extra machinery.
- The per-strategy `Deserialize<PeriodReportData>(Data)` call is a one-liner; failure surfaces as `JsonException`, which the strategy translates to a `400`-style validation error per spec FR-029 (out of scope for this feature, in scope for the period-report-strategy feature).

**Alternatives considered**:
- **Discriminated-union sealed-abstract record + per-type subtype** (`ReportRequest` abstract, `PeriodReportRequest(PeriodReportData Data) : ReportRequest`, `IsoWeekReportRequest(IsoWeekReportData Data) : ReportRequest`). Rejected because (a) it forces the per-type DTO into the envelope's structure (the spec explicitly factors the two apart at FR-011 vs FR-012/013), (b) `System.Text.Json` polymorphic deserialization with a discriminator requires `[JsonDerivedType]` attributes that pollute the type with serializer concerns, and (c) ASP.NET model binding for polymorphic request bodies is finicky and varies by `System.Text.Json` version.
- **`object Data` with a custom `JsonConverter<ReportRequest>` that dispatches on `Type`**. Rejected because the custom converter is materially more code than the strategy-local `Deserialize<T>` call, and the type safety it buys (compile-time-known `Data` type) isn't useful in the strategy: the strategy already needs a switch on `Type` to know which DTO it wants.
- **`Dictionary<string, object>` for `Data`**. Rejected because (a) it loses type/precision information (every leaf becomes `JsonElement` or boxed primitive anyway) and (b) it would require manual key validation that `JsonSerializer.Deserialize<T>` already does correctly.

**Consequences for this feature**:
- The `Finance.Business` project gains an `using System.Text.Json` directive in `Dtos/Reports/ReportRequest.cs`.
- `ReportRequest` round-trips losslessly because `JsonElement` is preserved verbatim across serialize/deserialize.
- Tests for `ReportRequest` will construct it both from a JSON string (deserialize-then-inspect) and programmatically (with `JsonDocument.Parse(...).RootElement.Clone()` for the `Data`) to exercise both paths.

---

## 2. JSON serialization configuration for enums, dates, and decimals

**Decision**: Centralize a single `JsonSerializerOptions` instance for the whole API:
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` — turns `IncomeTotal` into `"incomeTotal"`, matching the README's example payloads (FR-027 wire-format parity).
- `Converters` contains a `JsonStringEnumConverter` constructed *without* a naming policy, so enum values serialize as their declared C# names (`"Income"`, `"Expense"`, `"Both"`, `"Period"`, `"IsoWeek"`) — these are the exact strings the README documents.
- `DefaultIgnoreCondition = JsonIgnoreCondition.Never` — never silently omit fields. Empty collections serialize as `[]`, never absent.
- No `WriteIndented` toggle here (defaults to `false` for the API; tests opt in via a separate options instance when they want to compare against the README's pretty-printed examples).
- `Decimal` and `DateTime` use `System.Text.Json` defaults: `decimal` round-trips with full precision (System.Text.Json preserves trailing zeros via the underlying numeric grammar), and `DateTime` serializes as ISO 8601 with second-level precision when constructed with no sub-second component (and with sub-second when present — irrelevant here because the spec sets second precision as the contract).

**Rationale**:
- The README example payloads use camelCase (`"incomeTotal"`, `"transactionType"`, `"categoryIds"`); switching the default naming policy is the cleanest match.
- `JsonStringEnumConverter` with no naming policy preserves the declared case of enum members — exactly what FR-026 round-trip needs ("the `ReportType` enum as its name"). The constructor overload that takes a `JsonNamingPolicy` would lowercase or camelCase the values; that's *not* what we want.
- `IgnoreCondition = Never` is the safest default for a contract-first project: empty arrays and zero-values should appear in the response so callers don't have to special-case absent fields. Spec FR-022 explicitly requires empty `categoryBreakdown` to be `[]`, not omitted.

**Alternatives considered**:
- Per-controller `JsonOptions` configuration. Rejected — there's exactly one set of options we want everywhere; centralizing it in a single `static JsonSerializerOptions Default = new() { ... }` in `Finance.Business` is simpler than per-controller wiring.
- `JsonSerializerDefaults.Web` preset. Rejected because it sets `PropertyNameCaseInsensitive = true` and various other read-side behaviors we don't need; we want explicit control of the small surface area.

**Consequences for this feature**:
- This feature adds `Finance.Business/JsonOptions.cs` (or `JsonSerializationOptions.cs`) with the single shared instance. Tests use it directly; the later feature that wires controllers will register it with `AddControllers().AddJsonOptions(...)` or the equivalent for minimal APIs.
- Tests for round-trip fidelity (`*RoundTripTests.cs`) all use this single options instance, ensuring the test bed sees the same serializer behavior production will.

---

## 3. Read-only collection type for record-typed attributes

**Decision**: `IReadOnlyList<T>` for every collection attribute on a record (Transaction.CategoryIds, TransactionResponse.Categories, ReportResult.CategoryBreakdown). Construct from `ImmutableArray<T>` when the call site can build all-at-once, or from `List<T>.AsReadOnly()` when the call site has a `List<T>` already. Never expose `List<T>` or `T[]` directly.

**Rationale**:
- `IReadOnlyList<T>` is the most widely-recognized "ordered, read-only" interface in `System.Collections.Generic`. It supports index access (`x.CategoryIds[0]`), `Count`, and enumeration — everything the spec needs without exposing mutators.
- `System.Text.Json` serializes `IReadOnlyList<T>` and deserializes JSON arrays into `List<T>`-implementing concrete types out of the box; no converter needed. Order is preserved (FR-026 explicitly requires this).
- Using the interface (not `ImmutableArray<T>` or `ReadOnlyCollection<T>`) keeps the records consumable by call sites that want to build collections incrementally before freezing them — a useful flexibility for the test code and the future mapper for "request → entity" path.

**Alternatives considered**:
- **`ImmutableArray<T>`**. Strongly considered. Pros: value-type, zero-copy slicing, the "strongest" immutability guarantee in BCL. Cons: deserialization requires a custom converter (System.Text.Json doesn't deserialize `ImmutableArray<T>` natively without `System.Text.Json` ≥ 8 + `[JsonInclude]` patterns); the API surface forces consumers into the `System.Collections.Immutable` namespace.
- **`ReadOnlyCollection<T>`**. Rejected — `IReadOnlyList<T>` is the modern, interface-based equivalent; `ReadOnlyCollection<T>` is a concrete class with no advantage here.
- **`IEnumerable<T>`**. Rejected because it doesn't expose `Count` or index access; downstream code would re-enumerate (potential allocation surprise) or cast to `IReadOnlyList<T>` anyway.

**Consequences for this feature**:
- `Transaction.CategoryIds`: `IReadOnlyList<int>`.
- `TransactionResponse.Categories`: `IReadOnlyList<CategorySummary>`.
- `ReportResult.CategoryBreakdown`: `IReadOnlyList<CategoryBreakdownItem>`.
- Mappers build a `List<T>` locally and return `.AsReadOnly()` (which is `IReadOnlyList<T>`-typed) when handing off, or return an `ImmutableArray<T>.Builder.ToImmutable()` cast to `IReadOnlyList<T>`. Tests assert the runtime type can be enumerated but cannot be mutated (no `Add` reachable through the static type).

---

## 4. `DateOnly` vs `DateTime` for `PeriodReportData.Start` / `.End`

**Decision**: `PeriodReportData.Start` and `.End` are `DateOnly` (not `DateTime`). `Transaction.Timestamp` is `DateTime` per the spec.

**Rationale**:
- The spec explicitly distinguishes the two: FR-001 says transactions carry a full second-precision `DateTime`; FR-012 says `Period.Start`/`End` are **calendar dates**. `DateOnly` (BCL since .NET 6) is the correct primitive — it has no time-of-day component and round-trips as `"yyyy-MM-dd"` in JSON without any custom converter.
- Using `DateOnly` makes "the period is interpreted as `[start 00:00:00, end 23:59:59]`" a property of the strategy, not of the contract. The contract carries the user's intent (a calendar range); the strategy applies the second-level resolution.
- `DateTime` for `Start`/`End` would make sub-day boundaries representable but never meaningful, opening a class of subtle bugs (caller sends `"2026-05-31T15:00:00"`; do we round up, down, error?).

**Alternatives considered**:
- **`DateTime` for everything**. Rejected — see above.
- **`string` for `Start`/`End`** (parse in the strategy). Rejected — the contract is "this is a date", not "this is a string that may or may not parse"; `DateOnly` carries that guarantee at the type level.

**Consequences for this feature**:
- `PeriodReportData.Start` and `.End`: `DateOnly`.
- `IsoWeekReportData.Week`: `string` — *deferred to the IsoWeek strategy feature* (the decision is recorded here for forward reference; this feature does not define the type).
- `Transaction.Timestamp`: `DateTime` (naive — no offset, single-user local clock per the spec's Assumptions block).
- JSON round-trip: `DateOnly` ↔ `"2026-05-31"`, `DateTime` ↔ `"2026-05-05T18:42:00"`. Both native to `System.Text.Json`.

---

## 5. Test framework choice — xUnit v3, no FluentAssertions

**Decision**: Tests use **xUnit v3** (the `xunit.v3` 3.2.2 package, `OutputType=Exe`) — which is what the user-scaffolded test projects under `src/backend/FinanceTracker/tests/` already use — with the **built-in `Xunit.Assert` API** for assertions. **No FluentAssertions** (and no third-party assertion library at all). Tests are split across four projects: `Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, and `Finance.Api.IntegrationTests`. This feature populates the first two; neither needs any package reference beyond `xunit.v3`.

**Rationale**:
- **xUnit v3** is a major release with a new in-process discovery model: test projects are executables (`OutputType=Exe`) with `XunitAutoGeneratedEntryPoint` rather than libraries hosted by a separate test host. The `[Fact]` / `[Theory]` author-side API is unchanged from v2, and Visual Studio Test Explorer / `dotnet test` both discover v3 tests natively. The constitution mandates "xUnit" without pinning a major version, so v3 satisfies the rule.
- **No FluentAssertions, no replacement library**: FluentAssertions v8 (Jan 2025) moved to a **commercial license** managed by Xceed; v7.x is the last release under the previous free Apache 2.0 terms but is no longer maintained against new .NET releases. The user chose to drop FluentAssertions entirely rather than pin to v7.x or pay for v8. The built-in `Xunit.Assert` API covers every assertion this feature needs:
  - Scalar equality: `Assert.Equal(expected, actual)`
  - Reference inequality / equality variants: `Assert.NotEqual`, `Assert.Same`, `Assert.NotSame`
  - Boolean: `Assert.True`, `Assert.False`
  - Null: `Assert.Null`, `Assert.NotNull`
  - Exceptions: `Assert.Throws<T>(() => …)`, `Assert.ThrowsAny<T>`
  - Collections: `Assert.Empty`, `Assert.Single`, `Assert.Equal(expectedSeq, actualSeq)`, `Assert.Collection(seq, e1 => …, e2 => …)` for per-element predicates
  - Strings: `Assert.Contains`, `Assert.StartsWith`, `Assert.Matches`
  - Type checks: `Assert.IsType<T>`, `Assert.IsAssignableFrom<T>`
- **Multi-project test layout**: per-production-project unit test projects (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`) plus a dedicated integration-tests project (`Finance.Api.IntegrationTests`) keep each project's references minimal (e.g., `Finance.Data.UnitTests` only references `Finance.Data`) and let integration tests live in a project that can take a `WebApplicationFactory` dependency later without polluting the unit projects. This is standard .NET practice and matches the user's scaffold.

**Alternatives considered**:
- **Stay on xUnit v2**. Rejected — the user has already scaffolded v3 projects with the `xunit.v3` package, and there's no behavior in this feature that v2 supports better than v3. Forcing v2 would mean re-scaffolding.
- **Pin FluentAssertions v7.x** (the last free release). Rejected by the user — depending on an unmaintained release isn't worth the fluent assertion syntax for a pet project. Plain `Xunit.Assert` is the minimum-dependency answer.
- **Adopt FluentAssertions v8 (commercial)**. Rejected — license cost isn't justified by features for a single-developer pet project.
- **Replace FluentAssertions with Shouldly, AwesomeAssertions, or TUnit's assertions**. Rejected — each adds a third-party dependency. The user wants the minimum surface; `Xunit.Assert` ships with the test framework itself.
- **A single `Finance.Tests` project** (as the constitution currently mandates). Rejected because the user has already scaffolded four projects. Constitution drift #6 in [plan.md](plan.md#complexity-tracking) covers this and the assertion-library choice together.

**Consequences for this feature**:
- The two populated test projects each get only a `<ProjectReference>` to the production projects under test. No new NuGet package references beyond what the user already scaffolded.
- Test files use `using Xunit;` and `Assert.Equal(...)`-style assertions; no `value.Should().Be(...)` calls. The reference test list in the existing AI-artifact specs (`ai-artifacts/Specifications/*.md`) is still phrased in FluentAssertions terms — those specs are stale relative to this design and will be reconciled as part of their corresponding feature slices (see the CLAUDE.md divergence note at the top of the file).
- The constitution requires an amendment (drift #6 in [plan.md](plan.md#constitution-amendment-required-before-speckit-tasks)) to acknowledge the multi-project layout, the xUnit v3 pin, and the removal of FluentAssertions.

---

## Summary

All five design questions are now resolved. No outstanding `NEEDS CLARIFICATION` markers. Proceed to Phase 1 ([data-model.md](data-model.md) and [contracts/](contracts/)).
