# Implementation Plan: Domain Entities and API DTOs

**Branch**: `001-domain-entities-dtos` | **Date**: 2026-05-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-domain-entities-dtos/spec.md`

## Summary

Deliver the **data contracts** the rest of the MVP will build on: the `Transaction` / `Category` domain entities in `Finance.Data`, their related enums (`TransactionType`, `CategoryType`, `ReportType`), the request/response DTOs in `Finance.Business` (transaction + category CRUD shapes, the typed `ReportRequest` envelope with `PeriodReportData`, and the summary-style `ReportResult` + `CategoryBreakdownItem`), and the trivial 1:1 mappers between entities and DTOs. No behavior: no repositories, no controllers, no aggregation, no validation enforcement, no export. The next feature (in-memory repositories) and the one after (transactions/categories endpoints and the period-report strategy) consume these contracts.

> **MVP scope clarification (2026-05-21)**: only the `Period` report type has a defined payload DTO in this feature. `ReportType.IsoWeek` remains in the enum as a *documented future value* (the README's `ReportResult` example uses it, which is the US5 test fixture), but `IsoWeekReportData`, its JSON Schema branch, and the `IsoWeekReportStrategy` ship together with the IsoWeek feature later. See [spec.md User Story 6](spec.md#user-story-6---submit-a-report-request-with-a-typed-report-type-and-a-per-type-data-payload-priority-p3) for the scope note this revision is based on.

Technical approach: every contract is a C# `record` (positional, `init`-only by construction), targeting `net10.0`. The polymorphic `ReportRequest.Data` payload is modeled as `JsonElement` so each strategy can deserialize its own typed shape (`PeriodReportData` in this feature) lazily — this matches what `ai-artifacts/Specifications/period-report-strategy-spec.md` describes and avoids a custom polymorphic JSON converter for the MVP. Mappers live next to the DTOs they target and accept the data they need as plain arguments (e.g., `TransactionMapper.ToResponse(Transaction, IReadOnlyDictionary<int, Category>)`); they do not pull from repositories, so they remain pure and trivially testable.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`). Nullable reference types and implicit usings are already enabled in both projects' csproj files.

**Primary Dependencies**: `System.Text.Json` (built-in) for serialization with `JsonStringEnumConverter` for the three enums; `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` already present in `Finance.Api` for OpenAPI rendering when consumers ship endpoints later. No additional NuGet packages required for this feature.

**Storage**: N/A. This feature defines the shapes only; repositories and seeding land in the next feature.

**Testing**: **xUnit v3** (`xunit.v3` package, version 3.2.2 — test projects are `OutputType=Exe` with the v3 auto-generated entry point) using the **built-in `Xunit.Assert` API** for assertions. **No FluentAssertions** (constitution drift; see [research.md §5](research.md#5-test-framework-choice-xunit-v3-no-fluentassertions)). Tests live under `src/backend/FinanceTracker/tests/` in **four** projects: `Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, and `Finance.Api.IntegrationTests`. All four already exist on disk as bare scaffolds; this feature populates the first two (the API ones get tests only when controllers/endpoints ship in later features). Test coverage for this feature: record construction, immutability, mapper correctness, JSON round-trip fidelity (`DateTime` second precision, `decimal` precision, enum-name preservation, ordered collections), and the `JsonElement` carry-through on `ReportRequest`.

**Target Platform**: Cross-platform .NET 10 runtime. Development host is Windows 11 + PowerShell; CI will run on the GitHub-hosted Ubuntu runner once `.github/workflows/ci.yml` lands.

**Project Type**: Web service (ASP.NET Core Web API), but this feature touches only `Finance.Data` (entities + enums), `Finance.Business` (DTOs + mappers + the `ReportType` enum), and the new `Finance.Tests` project. `Finance.Api` is untouched.

**Performance Goals**: N/A for this feature — no runtime behavior. The records and mappers are zero-allocation hot paths anyway; no perf budget is needed.

**Constraints**: Every contract MUST be immutable by construction (records with `init`-only properties or positional parameters). Every collection-typed attribute on a record MUST be exposed as a read-only / immutable collection type (`IReadOnlyList<T>` chosen — see [research.md](research.md)). JSON round-trip MUST preserve attribute names, `decimal` precision, `DateTime` second precision, enum names, and collection order.

**Scale/Scope**:
- ~9 records, 3 enums, 2 mapper classes across two production projects (full inventory in [data-model.md](data-model.md)).
- ~12 unit tests distributed across `Finance.Data.UnitTests` (~3 tests on `Transaction` / `Category`) and `Finance.Business.UnitTests` (~9 tests on DTOs, mappers, and JSON round-trip). `Finance.Api.UnitTests` and `Finance.Api.IntegrationTests` exist but get no test files this feature.
- Net source code: ~250 lines + ~300 lines of tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Result of this gate**: ✅ **All five principles PASS against `.specify/memory/constitution.md` v2.0.0.** The six drifts originally identified against v1.0.0 of the constitution were ratified by the v2.0.0 amendment on 2026-05-19; the principles now describe the design this feature implements. The historical [Complexity Tracking](#complexity-tracking) and [Constitution Amendment Required](#constitution-amendment-required-before-speckit-tasks) sections are retained below as a record of the amendment's input.

Per-principle check (vs. constitution v2.0.0):

| Principle | Status | Notes |
|---|---|---|
| I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) | ✅ **PASS** | `Api → Business → Data` dependency direction preserved. DTOs live in `Finance.Business` (as v2.0.0 mandates), `Finance.Api` reuses them and never sees a domain entity, and the mappers + centralized `JsonSerializationOptions` are owned by Business. |
| II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) | ✅ **PASS** | Single endpoint + `ReportStrategyFactory` + `IReportStrategy` preserved. Envelope is the typed `{ ReportType, JsonElement Data }` shape with per-type `PeriodReportData` / `IsoWeekReportData` DTOs. `ReportResult` is the summary envelope (`type`, `period` descriptor, three totals, `categoryBreakdown` with signed totals). Multi-category attribution, income-first/expense-second/alphabetical sort, HTTP 400 mapping — all aligned with v2.0.0. |
| III. Seeded In-Memory Determinism (NON-NEGOTIABLE) | ✅ **PASS** | Singleton-lifetime repositories planned (this feature defines the shapes the next feature will store); deterministic integer seed IDs replace the v1.0.0 Guids; full `DateTime` timestamp; multi-category transactions with the all-must-be-compatible rule; case-insensitive duplicate name rejection; `Amount > 0`; no `currency` field anywhere. All aligned. |
| IV. Test-First with xUnit v3 | ✅ **PASS** | Four test projects under `tests/` exist (Data/Business/Api unit + Api integration), all xUnit v3, all `OutputType=Exe`. This feature populates the first two with `Xunit.Assert`-based tests; no FluentAssertions anywhere. The Principle IV coverage bullets that depend on later features (repository behavior, period-report aggregation, export, MCP) remain assigned to those features. |
| V. AI-Assisted Development Transparency | ✅ **PASS** | `ai-artifacts/agent_log.txt` is being appended with every AI-driven change in this work stream. No sensitive data in fixtures. OpenAPI / Scalar endpoints remain dev-only (no change in this feature). |

**Conclusion**: ✅ All principles pass. `/speckit-tasks` is unblocked.

> **Historical note**: The amendment that produced constitution v2.0.0 was driven directly by the drifts captured in [Complexity Tracking](#complexity-tracking) and [Constitution Amendment Required](#constitution-amendment-required-before-speckit-tasks) below. Those sections are retained for the record but no longer represent open work.

## Project Structure

### Documentation (this feature)

```text
specs/001-domain-entities-dtos/
├── plan.md                        # This file
├── research.md                    # Phase 0 — design decisions for the four open questions
├── data-model.md                  # Phase 1 — concrete C# record / enum signatures
├── contracts/                     # Phase 1 — JSON Schemas for every DTO that crosses HTTP
│   ├── transaction-create-request.schema.json
│   ├── transaction-response.schema.json
│   ├── category-create-request.schema.json
│   ├── category-response.schema.json
│   ├── report-request.schema.json
│   └── report-result.schema.json
├── quickstart.md                  # Phase 1 — how to build/test what this feature delivers
├── checklists/
│   └── requirements.md            # From /speckit-specify
├── spec.md                        # From /speckit-specify
└── tasks.md                       # Phase 2 (from /speckit-tasks, NOT created here)
```

### Source Code (repository root)

```text
src/backend/FinanceTracker/
├── FinanceTracker.slnx                # Already references all 3 production + 4 test projects
├── Finance.Api/                       # Untouched by this feature
├── Finance.Business/
│   ├── Finance.Business.csproj        # Existing — add <ProjectReference> to Finance.Data
│   ├── Enums/
│   │   └── ReportType.cs              # NEW — { Period, IsoWeek }
│   ├── Dtos/
│   │   ├── Transactions/
│   │   │   ├── TransactionCreateRequest.cs
│   │   │   ├── TransactionResponse.cs
│   │   │   └── CategorySummary.cs     # nested { Id, Name } shape used by TransactionResponse
│   │   ├── Categories/
│   │   │   ├── CategoryCreateRequest.cs
│   │   │   └── CategoryResponse.cs
│   │   └── Reports/
│   │       ├── ReportRequest.cs       # { ReportType Type, JsonElement Data }
│   │       ├── PeriodReportData.cs    # { DateOnly Start, DateOnly End }
│   │       │                          # IsoWeekReportData.cs — DEFERRED to a later feature
│   │       ├── CategoryBreakdownItem.cs
│   │       └── ReportResult.cs
│   ├── JsonSerializationOptions.cs    # NEW — static class { JsonSerializerOptions Default }
│   └── Mappers/
│       ├── TransactionMapper.cs       # entity ↔ request/response (1:1, no repo access)
│       └── CategoryMapper.cs
├── Finance.Data/
│   ├── Finance.Data.csproj            # Existing — no new references
│   └── Models/
│       ├── Transaction.cs             # { int Id, DateTime Timestamp, string Description,
│       │                              #   decimal Amount, TransactionType Type,
│       │                              #   IReadOnlyList<int> CategoryIds }
│       ├── Category.cs                # { int Id, string Name, CategoryType Type }
│       ├── TransactionType.cs         # enum { Income, Expense }
│       └── CategoryType.cs            # enum { Income, Expense, Both }
└── tests/                             # All four test projects already scaffolded on disk
    ├── Finance.Data.UnitTests/
    │   ├── Finance.Data.UnitTests.csproj    # Existing — xunit.v3 3.2.2, OutputType=Exe.
    │   │                                    # ADD: <ProjectReference> to Finance.Data.
    │   │                                    # NO FluentAssertions; use Xunit.Assert.
    │   ├── TransactionTests.cs              # NEW
    │   └── CategoryTests.cs                 # NEW
    ├── Finance.Business.UnitTests/
    │   ├── Finance.Business.UnitTests.csproj # Existing — same setup as above.
    │   │                                     # ADD: <ProjectReference>s to Finance.Business + Finance.Data.
    │   │                                     # NO FluentAssertions; use Xunit.Assert.
    │   ├── Dtos/
    │   │   ├── TransactionResponseRoundTripTests.cs  # NEW
    │   │   ├── ReportRequestRoundTripTests.cs        # NEW
    │   │   └── ReportResultRoundTripTests.cs         # NEW
    │   └── Mappers/
    │       ├── TransactionMapperTests.cs    # NEW
    │       └── CategoryMapperTests.cs       # NEW
    ├── Finance.Api.UnitTests/               # Scaffolded; no tests yet (no API code in scope).
    │   └── Finance.Api.UnitTests.csproj     # Existing — untouched by this feature.
    └── Finance.Api.IntegrationTests/        # Scaffolded; no tests yet (no endpoints in scope).
        └── Finance.Api.IntegrationTests.csproj  # Existing — untouched by this feature.

.github/
└── workflows/                         # Untouched by this feature
                                       # (ci.yml lands in a later feature)
```

**Structure Decision**: Three production projects + four test projects already exist under `src/backend/FinanceTracker/` (the test projects under `tests/` were scaffolded by the user just before this update — they're bare xUnit v3 stubs with no `ProjectReference`s). This feature adds source files inside `Finance.Data/Models/` (entities + enums), inside new `Finance.Business/` subfolders (`Enums/`, `Dtos/Transactions/`, `Dtos/Categories/`, `Dtos/Reports/`, `Mappers/`, plus the centralized `JsonSerializationOptions.cs`), and inside two of the four test projects (`Finance.Data.UnitTests/` and `Finance.Business.UnitTests/`). The remaining two test projects (`Finance.Api.UnitTests/`, `Finance.Api.IntegrationTests/`) stay empty — they get populated when controllers and endpoints ship in later features. No new top-level layout. The `Finance.Business → Finance.Data` project reference needs to be added to `Finance.Business.csproj` (entities live in Data; DTOs+mappers in Business need to see them); the two production-project references need to be added to the two test projects this feature populates. **No additional NuGet packages** — assertions use the built-in `Xunit.Assert` API; no FluentAssertions, no Shouldly, no AwesomeAssertions.

## Complexity Tracking

The five drifts below are all user-directed design changes captured across the spec's post-feedback revisions (see [checklists/requirements.md](checklists/requirements.md) for the trail). They are listed so the constitution-amendment PR (run `/speckit-constitution`) can use this table verbatim.

| Drift | Constitution principle | Current spec/README/CLAUDE.md | Why needed | Simpler alternative rejected because |
|---|---|---|---|---|
| #1 DTOs in Business layer | I. Three-Layer Architecture Boundaries — bullet says `Finance.Api owns ... DTOs` | DTOs (request, response, report contracts) live in `Finance.Business`; `Finance.Api` reuses them and only adds API-specific models when needed | User wants the Business layer to be the boundary that owns "what the application accepts and emits", and the API layer to be a thin transport adapter. Mappers between Data entities and DTOs sit naturally next to the DTOs in Business. | Putting DTOs in API forces every Business-layer service to either accept Data entities (leaking the layer) or re-define the same shapes — the duplication is what the user wanted to avoid. |
| #2 `ReportRequest` envelope shape | II. Report Factory + Strategy Pattern — bullet says envelope is `{ "type": <string>, "parameters": <object> }` | Envelope is `{ Type: ReportType-enum, Data: JsonElement }` with per-type DTOs `PeriodReportData { Start, End }` and `IsoWeekReportData { Week }` | Enum-typed `Type` rejects unknown report types at the binding layer (vs. string + factory failure). `data` (typed) replaces the previous opaque `parameters` so each strategy's contract is discoverable from the type system. | Keeping `string + object` works but pushes the type-validity check into the factory at runtime; users have to read the factory to know what types exist. The enum-driven shape is a strict refinement. |
| #3 `ReportResult` shape & sort invariants | II. Report Factory + Strategy Pattern — amounts/sort bullets reference `transactions` array, `incomeTotal`/`expenseTotal`/`netTotal`, and `categoryBreakdown` sorted by transaction type then category name | `ReportResult` is summary-only: `{ Type, Period (string), IncomeTotal, ExpenseTotal, NetTotal, CategoryBreakdown }`. No `Transactions` array. `CategoryBreakdownItem` is `{ Category, Total (signed) }` — no transaction count, no direction field. Sorted income-side first then expense-side, alphabetical within each. | The user explicitly chose this shape over both (a) just-the-transactions and (b) the original full-payload-with-transactions, after seeing all three. It matches the textual "Weekly summary" output they illustrated. Multi-category attribution rule (full credit to each tagged category) is documented in spec FR-020. | Returning `transactions` alongside the summary doubles the response size for what callers can compute or re-fetch. The original sort by `transactionType` + name was redundant once the breakdown groups by sign of total. |
| #4 `int` identifiers | III. Seeded In-Memory Determinism — bullet says "deterministic GUIDs" | Identifiers are `int` for both `Transaction.Id` and `Category.Id`. Seed data uses fixed integers (1, 2, 3, …) for the same determinism property. | The MVP is single-user, in-memory, no distribution; `Guid`s buy nothing here and make URLs and test assertions noisy. The determinism property — "tests can assert on specific identifiers" — is identical with `int`s. | `Guid` works but adds 16 bytes/id and ugly assertion strings (`"11111111-..."`). The constitution's rationale for determinism is unchanged; only the encoding changes. |
| #5 Multi-category transactions | III. Seeded In-Memory Determinism — bullet says `A transaction's TransactionType MUST be compatible with its category's CategoryType (or the category MUST be Both)` (single-category framing) | `Transaction.CategoryIds` is a non-empty `IReadOnlyList<int>`. Compatibility rule: every category must be compatible with the transaction's `TransactionType`. | The user identified "one transaction can have a few categories" as an important detail (a real-world transaction can fit multiple categories — e.g., Groceries + Health Food). | The single-category model forces awkward "primary" + "tags" hacks or splits one transaction into multiple. Both compromise the breakdown numbers. The multi-category model with full-attribution attribution is the clean answer. |
| #6 Test layout + assertion library | IV. Test-First with xUnit + FluentAssertions — "Tests MUST live in a dedicated `Finance.Tests` project ... using xUnit as the runner and FluentAssertions for assertions" | Three changes touching Principle IV at once: (a) Four test projects under `src/backend/FinanceTracker/tests/`: `Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, `Finance.Api.IntegrationTests` (not a single project). (b) **xUnit v3** (`xunit.v3` 3.2.2, `OutputType=Exe`) rather than an unspecified xUnit version. (c) **No FluentAssertions** — assertions use the built-in `Xunit.Assert` API. | (a) The user scaffolded the multi-project layout to isolate unit-test concerns per production project (each test project's references stay minimal) and to carve out a separate integration-tests project for the API (which the constitution's single-project rule could not express). (b) xUnit v3 is the package shape the user scaffolded — v2 → v3 is a behind-the-scenes change (Exe vs library hosting); the `[Fact]`/`[Theory]` author-side API is unchanged. (c) FluentAssertions v8 (Jan 2025) moved to a commercial license; v7.x is the last free release. The user explicitly chose to drop FluentAssertions entirely rather than pin to v7.x or buy a v8 license — `Xunit.Assert` covers everything the spec's required tests need (equality, exception assertion, collection assertion). | (a) A single `Finance.Tests` project mixes unit and integration tests, forcing transitive references and slower runs. (b) xUnit v2 is the older shape; the user has already scaffolded v3 and there's no behavior in this feature that benefits from forcing v2. (c) Pinning FluentAssertions v7.x means depending on a release that's no longer maintained; buying v8 isn't justified for a pet project. Plain `Xunit.Assert` is the minimum-dependency answer. |

## Constitution Amendment Required (before `/speckit-tasks`)

Run `/speckit-constitution` with the five drifts above. Suggested edits:

1. **Principle I**: move the DTO-ownership bullet from `Finance.Api` to `Finance.Business`. Add the API-layer language about "reuses Business DTOs; only adds API-specific models when needed."
2. **Principle II**: replace the envelope example with `{ "type": "Period", "data": { "start": "...", "end": "..." } }` and add an explicit reference to `ReportType` as an enum. Replace the aggregation invariants block with the new sort/shape rules: no `transactions` array, signed `CategoryBreakdownItem.total`, multi-category attribution, income-first / expense-second / alphabetical ordering.
3. **Principle III**: change "deterministic GUIDs" to "deterministic seed IDs (integers)". Replace the single-category compatibility bullet with the multi-category "every attached category must be compatible" rule.
4. **Principle IV**: change "Tests MUST live in a dedicated `Finance.Tests` project ... using xUnit as the runner and FluentAssertions for assertions" to "Tests MUST live in one or more dedicated test projects under `src/backend/FinanceTracker/tests/`, organized per production project (unit) plus a dedicated integration-tests project for the API, using **xUnit v3** as the runner and the **built-in `Xunit.Assert` API** for assertions. FluentAssertions is NOT used in this project." The Principle IV "Rationale" paragraph that says "FluentAssertions is mandated for consistency of failure messages and because the period-report-strategy-spec is written assuming it" should be replaced with "Assertion style is unified by the spec's reference test list (in the per-feature ai-artifact specs), not by a particular library; the project uses plain `Xunit.Assert` to keep the dependency surface minimal and avoid the FluentAssertions v7 → v8 commercial-license cliff."
5. Bump version to **2.0.0** (MAJOR — NON-NEGOTIABLE principles are being redefined by drifts #1, #2, #4, #5; drift #6 is PATCH-level wording but rides along in the same amendment). `Last Amended` = today's date. Update the Sync Impact Report with the six drifts.
6. After the amendment lands, this plan's Constitution Check should re-evaluate to all-PASS without any drift entries.
