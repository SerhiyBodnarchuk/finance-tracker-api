# Quickstart — Domain Entities and API DTOs

**Feature**: `001-domain-entities-dtos` · **Plan**: [plan.md](plan.md)

This document is the "what to do, in order" view of the feature. It assumes [data-model.md](data-model.md) for the concrete record signatures, [research.md](research.md) for the design decisions, and [contracts/](contracts/) for the wire shapes. The numbered steps are dependency-ordered; tasks within a numbered step can be parallelized.

## Prerequisites

```powershell
# From repo root:
cd src/backend/FinanceTracker
dotnet --info        # confirms .NET 10 SDK is installed
dotnet restore       # should succeed against the existing solution
dotnet build         # should succeed; current state has only the Finance.Api scaffold
```

If any of the above fails, stop and resolve before continuing — there's no code change that fixes a missing SDK.

## What this feature delivers

When done, the following are true:

- `Finance.Data` exposes `Transaction`, `Category`, `TransactionType`, `CategoryType` as `public sealed record` / `enum`.
- `Finance.Business` exposes the `ReportType` enum (declares `Period` and `IsoWeek`; only `Period` has a payload DTO in this feature — `IsoWeekReportData` is deferred to the IsoWeek strategy feature), all the request/response DTOs (`TransactionCreateRequest`/`TransactionResponse`/`CategoryCreateRequest`/`CategoryResponse`/`ReportRequest`/`PeriodReportData`/`ReportResult`/`CategoryBreakdownItem`/`CategorySummary`), the centralized `JsonSerializationOptions.Default`, and the two mapper classes (`TransactionMapper`, `CategoryMapper`).
- `Finance.Business` references `Finance.Data`.
- The two test projects this feature populates (`tests/Finance.Data.UnitTests`, `tests/Finance.Business.UnitTests`) reference the production projects they cover and have `FluentAssertions` installed. The other two (`tests/Finance.Api.UnitTests`, `tests/Finance.Api.IntegrationTests`) are still bare scaffolds.
- ~12 passing tests cover record construction, immutability, mapper correctness, and JSON round-trip fidelity, distributed across the two populated test projects.
- `dotnet build` and `dotnet test` both succeed at the solution root.

The following are **not** true (intentionally — those are subsequent features):

- No repositories, no seed data.
- No controllers, no endpoints, no HTTP behavior.
- No validation enforcement (e.g., rejecting `amount <= 0`).
- No report-strategy execution.
- No JSON or CSV export.

## Steps

### 1. Add the `Finance.Business → Finance.Data` project reference

`Finance.Business.csproj` does not currently reference `Finance.Data`, but the DTOs and mappers introduced here transitively need to see `Transaction` and `Category`. Add the project reference once before writing any source files. This is a one-line addition to the csproj's `<ItemGroup>` block.

### 2. Add the domain types in `Finance.Data`

Create `src/backend/FinanceTracker/Finance.Data/Models/` and add:

- `TransactionType.cs` — enum `{ Income, Expense }`.
- `CategoryType.cs` — enum `{ Income, Expense, Both }`.
- `Transaction.cs` — `public sealed record Transaction(int Id, DateTime Timestamp, string Description, decimal Amount, TransactionType Type, IReadOnlyList<int> CategoryIds);`
- `Category.cs` — `public sealed record Category(int Id, string Name, CategoryType Type);`

The exact signatures are in [data-model.md](data-model.md). No bodies, no methods — these records are pure shapes.

### 3. Add the enum + DTOs + JSON options in `Finance.Business`

Under `src/backend/FinanceTracker/Finance.Business/`:

- `Enums/ReportType.cs` — enum `{ Period, IsoWeek }`.
- `Dtos/Transactions/TransactionCreateRequest.cs`, `TransactionResponse.cs`, `CategorySummary.cs`.
- `Dtos/Categories/CategoryCreateRequest.cs`, `CategoryResponse.cs`.
- `Dtos/Reports/ReportRequest.cs`, `PeriodReportData.cs`, `CategoryBreakdownItem.cs`, `ReportResult.cs`. **No `IsoWeekReportData.cs`** in this feature — it ships with the IsoWeek strategy later.
- `JsonSerializationOptions.cs` — `public static class JsonSerializationOptions { public static JsonSerializerOptions Default { get; } = new() { … }; }` with the configuration from [research.md §2](research.md#2-json-serialization-configuration-for-enums-dates-and-decimals).

### 4. Add the mappers in `Finance.Business`

Under `Finance.Business/Mappers/`:

- `CategoryMapper.cs` — `ToResponse(Category) → CategoryResponse` and `ToEntity(CategoryCreateRequest, int) → Category`.
- `TransactionMapper.cs` — `ToResponse(Transaction, IReadOnlyDictionary<int, Category>) → TransactionResponse` and `ToEntity(TransactionCreateRequest, int) → Transaction`.

Full bodies are in [data-model.md](data-model.md). Both mappers are `static class` with `static` extension methods (`this Category category`, etc.).

### 5. Wire up the existing test projects

Four test projects already exist under `src/backend/FinanceTracker/tests/` (scaffolded by the user just before this implementation; they're bare xUnit v3 stubs — `OutputType=Exe`, `xunit.v3` 3.2.2, no project references). This feature only populates two of the four; the other two stay empty until later features add API code.

For each of `Finance.Data.UnitTests` and `Finance.Business.UnitTests`, add the project references they need (no additional NuGet packages):

```powershell
cd src/backend/FinanceTracker

# Finance.Data.UnitTests sees only the Data project.
dotnet add tests/Finance.Data.UnitTests/Finance.Data.UnitTests.csproj reference Finance.Data/Finance.Data.csproj

# Finance.Business.UnitTests sees both Business and Data (Business already references Data,
# so the second one is for direct construction of Transaction/Category in test arranges).
dotnet add tests/Finance.Business.UnitTests/Finance.Business.UnitTests.csproj reference Finance.Business/Finance.Business.csproj Finance.Data/Finance.Data.csproj
```

Notes on the test framework:

- **xUnit v3** uses `[Fact]` / `[Theory]` the same way v2 does, but discovery is in-process (no separate test host). The xUnit v3 + .NET 10 combo is supported by Visual Studio Test Explorer and `dotnet test`.
- **Assertions use the built-in `Xunit.Assert` API** — `Assert.Equal`, `Assert.True`, `Assert.Throws<T>`, `Assert.Collection`, `Assert.IsType<T>`, etc. **No FluentAssertions, no Shouldly, no third-party assertion library**. See [research.md §5](research.md#5-test-framework-choice-xunit-v3-no-fluentassertions) for why.
- `Finance.Api.UnitTests` and `Finance.Api.IntegrationTests` stay untouched this feature. Don't add references or test files to them yet.

### 6. Add the tests

Distribute the test files across the two populated projects (paths from `src/backend/FinanceTracker/`):

**`tests/Finance.Data.UnitTests/`** — pure entity coverage:

- `TransactionTests.cs`
  - constructs a `Transaction` with all fields populated and asserts each field round-trips
  - constructs two transactions with the same data but different `Id`s and asserts they are *not* equal (records use structural equality including `Id`)
  - asserts `CategoryIds` is statically typed as `IReadOnlyList<int>` (compile-time check expressed via the static type of a local + `Assert.IsAssignableFrom<IReadOnlyList<int>>(value)` runtime check)
  - asserts a transaction tagged with two categories preserves the order of `CategoryIds` exactly
- `CategoryTests.cs`
  - constructs each of the three `CategoryType` variants and confirms each is exposed exactly as supplied
  - constructs two categories whose names differ only by case (`"Groceries"` vs `"groceries"`) and asserts they compare equal under `StringComparer.OrdinalIgnoreCase` (the entity stores the name as-supplied; case-insensitive *equivalence* is the consumer's check)

**`tests/Finance.Business.UnitTests/`** — DTO round-trip + mapper coverage:

- `Dtos/TransactionResponseRoundTripTests.cs`
  - serializes a `TransactionResponse` with two categories using `JsonSerializationOptions.Default`
  - asserts the produced JSON matches the README's documented example (field names, ordering inside `categories`, `decimal` precision on `amount`, second precision on `timestamp`, `transactionType` rendered as `"Expense"`)
  - deserializes back and asserts field-by-field equality with the original
- `Dtos/ReportRequestRoundTripTests.cs`
  - serializes a `ReportRequest(ReportType.Period, …)` constructed from a `PeriodReportData`
  - deserializes the JSON back into a `ReportRequest`, calls `JsonSerializer.Deserialize<PeriodReportData>(req.Data, options)` on the `JsonElement`, asserts the fields match the original
  - asserts an unknown enum value (`"type": "Quarter"`) throws `JsonException` during deserialization (covers FR-006's closed-set rejection — `IsoWeek` is a *valid* enum value in this feature, so use `Quarter` or another absent value for the rejection test)
  - (IsoWeek payload round-trip is deferred — `IsoWeekReportData` ships with the IsoWeek strategy)
- `Dtos/ReportResultRoundTripTests.cs`
  - constructs a `ReportResult` matching the README's ISO-week example (2026-W20, $1200 / $430.50 / $769.50, five-item `categoryBreakdown`)
  - asserts the serialized JSON matches the README's example field-for-field (including the sort order of `categoryBreakdown`)
  - constructs an empty-period `ReportResult` (zero totals, empty breakdown) and asserts the JSON shows `"categoryBreakdown": []` rather than `null` or absent
- `Mappers/CategoryMapperTests.cs`
  - `ToResponse` round-trips the three fields
  - `ToEntity` carries the assigned id through
- `Mappers/TransactionMapperTests.cs`
  - `ToResponse` maps the six scalar fields + builds the `Categories` collection in `CategoryIds` order
  - `ToResponse` against a `categoriesById` dictionary missing one of the referenced ids throws `KeyNotFoundException` with the offending transaction id and category id in the message
  - `ToEntity` carries the assigned id through and preserves the `CategoryIds` reference

### 7. Verify

```powershell
cd src/backend/FinanceTracker
dotnet build
dotnet test
```

Both must succeed with zero warnings (nullable warnings included — the records are non-nullable on every field except where the spec explicitly allows null, which is nowhere for the MVP).

## How downstream features consume this

For the next feature (in-memory repositories):

- `InMemoryTransactionRepository` and `InMemoryCategoryRepository` live in `Finance.Data`. They expose `IEnumerable<Transaction>` / `IEnumerable<Category>` and singleton-scoped `Add` / `Get` / `Delete` methods.
- Seed data uses fixed `int` ids and constructs the entities directly through the records' positional constructors.
- The repositories return entities (never DTOs).

For the feature after that (transactions/categories endpoints):

- Controllers receive `TransactionCreateRequest` / `CategoryCreateRequest` via model binding.
- A small service or the controller itself uses `request.ToEntity(nextId)` to materialize an entity, then hands it to the repository.
- For responses, the service calls `transaction.ToResponse(categoriesById)` after `categoriesById` has been built from a single repository fetch. The controller returns the `TransactionResponse` / `CategoryResponse` directly.

For the period-report strategy feature:

- `PeriodReportStrategy.Generate(ReportRequest)` calls `JsonSerializer.Deserialize<PeriodReportData>(request.Data, JsonSerializationOptions.Default)` to get the typed `Start` / `End`.
- It filters transactions whose `Timestamp.Date` is in `[Start, End]` inclusive, computes the income/expense/net totals, builds the `CategoryBreakdown` per the multi-category attribution rule in [spec.md](spec.md#requirements-mandatory) FR-020, sorts per FR-021, and returns a `ReportResult` with `Period = $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd}"`.

## When you're done

- `git status` shows new files under `src/backend/FinanceTracker/Finance.Data/Models/`, `src/backend/FinanceTracker/Finance.Business/Enums/`, `src/backend/FinanceTracker/Finance.Business/Dtos/**`, `src/backend/FinanceTracker/Finance.Business/Mappers/`, `src/backend/FinanceTracker/Finance.Business/JsonSerializationOptions.cs`, plus new test source files inside the existing `tests/Finance.Data.UnitTests/` and `tests/Finance.Business.UnitTests/` projects (and modified csproj files in each of those for the added references/packages).
- `dotnet test` passes.
- Append a single entry to [`ai-artifacts/agent_log.txt`](../../ai-artifacts/agent_log.txt) for the implementation run.
- Run `/speckit-tasks` for the dependency-ordered task list and `/speckit-implement` to drive the actual code work. (Both should wait until the constitution amendment described in [plan.md](plan.md#constitution-amendment-required-before-speckit-tasks) lands.)
