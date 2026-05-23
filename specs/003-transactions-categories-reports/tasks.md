---

description: "Task list for feature 003-transactions-categories-reports"
---

# Tasks: Transactions / Categories Endpoints with Business Validation and Period Reports

> **Post-implementation amendment (2026-05-21)**: A brief round-trip happened after the original 32 tasks completed. The user first removed the `Microsoft.AspNetCore.Mvc.Testing` package addition from `Finance.Api.IntegrationTests.csproj` (deliberate), which forced the four integration test files (`ApiTestFixture.cs`, `TransactionsEndpointsTests.cs`, `CategoriesEndpointsTests.cs`, `ReportsEndpointTests.cs`) and the supporting `Program.cs` hooks to be deleted to keep the build green. The user then directed "Add integration tests. That's required." — so the package, the four test files, and the `Program.cs` hooks (`UseEnvironment("Testing")` gate on `UseHttpsRedirection`, `public partial class Program;`) were all restored. Final state: 70 tests passing (22 Data + 32 Business + 16 Integration), `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 is back in `Finance.Api.IntegrationTests.csproj`, all task descriptions below remain accurate to what is on disk. The round-trip is preserved in the agent log under Principle V.

**Input**: Design documents from `/specs/003-transactions-categories-reports/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/http-endpoints.md](contracts/http-endpoints.md), [quickstart.md](quickstart.md)

**Tests**: REQUIRED. Constitution Principle IV (Test-First with xUnit v3) mandates the coverage areas explicitly assigned to this feature: validation, factory selection by enum value, period aggregation including the multi-category attribution invariant and the boundary edge cases, category breakdown sort order, and API endpoint integration coverage. All tests use `Xunit.Assert` only — no FluentAssertions (constitution Principle IV). Tests are written before their corresponding implementation tasks within each phase.

**Organization**: Tasks are grouped by user story (US1, US2, US3 from [spec.md](spec.md)). Each story is implementable + verifiable as an independent slice. Implementation files that span stories (e.g., `TransactionsController.cs` carrying GET in US1 and POST/DELETE in US2) are extended in the later story rather than rewritten — this matches realistic TDD flow and is documented under [Within Each User Story](#within-each-user-story).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are repo-relative

## Path Conventions

ASP.NET Core Web API under `src/backend/FinanceTracker/`. Source under `Finance.Api/`, `Finance.Business/`, `Finance.Data/`. Tests under `src/backend/FinanceTracker/tests/`. Per [plan.md §Project Structure](plan.md#project-structure).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folder skeleton, scaffold cleanup, and the one csproj edit that integration tests depend on.

- [X] T001 Create directories: `src/backend/FinanceTracker/Finance.Business/Validation/`, `src/backend/FinanceTracker/Finance.Business/Reports/`, `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Validation/`, `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Reports/`. (Folders only — no files yet.) Verify `src/backend/FinanceTracker/Finance.Api/Controllers/` already exists.
- [X] T002 Delete `src/backend/FinanceTracker/Finance.Api/Controllers/WeatherForecastController.cs` and `src/backend/FinanceTracker/Finance.Api/WeatherForecast.cs` (the default `dotnet new webapi` scaffold; see [research.md §8](research.md#8-removing-the-default-weatherforecastcontroller-scaffold)).
- [X] T003 Modify `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/Finance.Api.IntegrationTests.csproj`: add `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />` and `<ProjectReference Include="..\..\Finance.Api\Finance.Api.csproj" />`. Run `dotnet restore` on the project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared test fixture that every integration test class consumes.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Create `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ApiTestFixture.cs`: a public class implementing `IClassFixture<>`-shaped wrapper around `WebApplicationFactory<Program>` (using `Microsoft.AspNetCore.Mvc.Testing`). Expose a single `HttpClient CreateClient()` method that returns a client with `Accept: application/json` set. Per [research.md §7](research.md#7-integration-test-infrastructure-choice).

**Checkpoint**: Foundation ready. Integration tests can be authored against `ApiTestFixture`. User story implementation can now begin.

---

## Phase 3: User Story 1 - Read transactions and categories over HTTP (Priority: P1) 🎯 MVP

**Goal**: Two GET-list and two GET-by-id endpoints work end-to-end — controller → mapper → repository → response DTO. 404 mapping for unknown ids returns RFC 7807 `ProblemDetails`.

**Independent Test**: With the API host running, `GET /api/transactions` and `GET /api/categories` each return 200 with their five seeded records in id-ascending order; `GET /api/transactions/3` returns 200 with the Silpo Market record; `GET /api/transactions/99999` and `GET /api/categories/99999` each return 404 with a `ProblemDetails` body.

### Tests for User Story 1

> Write these tests **first**; they must fail (404 because the controllers do not yet exist) before T009/T010 land.

- [X] T005 [P] [US1] Create `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/TransactionsEndpointsTests.cs` with three `[Fact]` tests against `ApiTestFixture`:
  - `GET_transactions_returns_200_with_five_seeded_records` — asserts status 200, body parses as `IReadOnlyList<TransactionResponse>`, `.Count == 5`, ids 1..5 in order, `Description` of id 1 is `"Monthly salary"`.
  - `GET_transactions_by_id_returns_200_for_seeded_id` — `GET /api/transactions/3` returns 200 with `Description == "Silpo Market"` and `Categories[0].Name == "Groceries"`.
  - `GET_transactions_by_id_returns_404_for_unknown_id` — `GET /api/transactions/99999` returns 404 with a `ProblemDetails` body whose `status: 404`.
- [X] T006 [P] [US1] Create `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/CategoriesEndpointsTests.cs` with three `[Fact]` tests:
  - `GET_categories_returns_200_with_five_seeded_records` — five `CategoryResponse` objects in id order.
  - `GET_categories_by_id_returns_200_for_seeded_id` — `GET /api/categories/2` returns 200 with `Name == "Groceries"`.
  - `GET_categories_by_id_returns_404_for_unknown_id` — `GET /api/categories/99999` returns 404.

### Implementation for User Story 1

- [X] T007 [P] [US1] Create `src/backend/FinanceTracker/Finance.Api/Controllers/TransactionsController.cs`. Decorate with `[ApiController]` and `[Route("api/[controller]")]`. Inject `ITransactionRepository` and `ICategoryRepository`. Implement `[HttpGet] List()` and `[HttpGet("{id:int}")] GetById(int id)` per [data-model.md §4](data-model.md#4-controllers). Use `transaction.ToResponse(categoriesById)` from feature 001's `TransactionMapper`. Return `NotFound(new ProblemDetails { Status = 404, Title = "Transaction not found", Detail = $"No transaction with id {id}." })` on `GetById` miss. `Add`/`Delete` methods are added in US2 — do NOT include them yet.
- [X] T008 [P] [US1] Create `src/backend/FinanceTracker/Finance.Api/Controllers/CategoriesController.cs`. Same shape as T007 but for categories: `[HttpGet] List()` and `[HttpGet("{id:int}")] GetById(int id)`. Inject `ICategoryRepository` only. Use `category.ToResponse()` from feature 001's `CategoryMapper`. `Add` method is added in US2 — do NOT include it yet.

**Checkpoint**: At this point, US1 is fully functional. The six US1 integration tests must all pass. `dotnet test --filter "Category=US1"` (or the explicit class-name filter) is green. The API host returns the seeded data over HTTP.

---

## Phase 4: User Story 2 - Create and delete with validation (Priority: P2)

**Goal**: `POST /api/transactions` validates input via the business layer and returns 201 on success / 400 on validation failure. `POST /api/categories` validates input and returns 201 / 400 / 409 (duplicate name). `DELETE /api/transactions/{id}` returns 204 / 404. All validation rules deferred from feature 002 (`Amount > 0`, non-empty `Description`, non-empty `CategoryIds`, every id exists, type-compat) are enforced in `TransactionValidator`.

**Independent Test**: Create a transaction with `amount: 100, type: "Expense", categoryIds: [2]` → expect HTTP 201 with `Location` header. Create with `amount: 0` → expect 400 with `errors.amount`. Create with `categoryIds: [9999]` → expect 400 with `errors.categoryIds[0]`. Create with `type: "Expense", categoryIds: [1]` (Salary is Income-only) → expect 400. Create a category named `"Savings"` → 201. Create another named `"SAVINGS"` → 409. Delete transaction id 4 → 204; same delete again → 404.

### Tests for User Story 2

> Write these tests **first**. Validator tests need the validator types to exist; integration tests need the controller methods to exist. Both will fail to compile / fail with 404 until the implementations land later in the phase.

- [X] T009 [P] [US2] Create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Validation/TransactionValidatorTests.cs` with eight `[Fact]` tests covering each FR-009..FR-013 failure mode plus the compatibility happy path. Construct the validator with an in-memory `InMemoryCategoryRepository` (the real one — no mock). Per [data-model.md §6](data-model.md#6-tests).
- [X] T010 [P] [US2] Create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Validation/CategoryValidatorTests.cs` with three `[Fact]` tests (valid happy path, empty name reject, whitespace-only name reject).
- [X] T011 [P] [US2] Extend `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/TransactionsEndpointsTests.cs` with three more `[Fact]` tests: `POST_transactions_returns_201_with_location_for_valid_body`, `POST_transactions_returns_400_with_amount_error_for_zero_amount`, `DELETE_transactions_id_returns_204_then_404_on_second_call`. Each constructs its own `WebApplicationFactory` so seeded state is pristine.
- [X] T012 [P] [US2] Extend `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/CategoriesEndpointsTests.cs` with two more `[Fact]` tests: `POST_categories_returns_201_for_valid_body`, `POST_categories_returns_409_for_case_insensitive_duplicate_name`.

### Implementation for User Story 2

- [X] T013 [P] [US2] Create `src/backend/FinanceTracker/Finance.Business/Validation/ValidationError.cs` and `src/backend/FinanceTracker/Finance.Business/Validation/ValidationResult.cs` per [data-model.md §1](data-model.md#1-validation-result-types). Two small records in the `Finance.Business.Validation` namespace.
- [X] T014 [P] [US2] Create `src/backend/FinanceTracker/Finance.Business/Validation/ITransactionValidator.cs` (interface) and `src/backend/FinanceTracker/Finance.Business/Validation/TransactionValidator.cs` (implementation) per [data-model.md §2](data-model.md#2-validator-services). Primary-constructor injection of `ICategoryRepository`. Compatibility helper inlined as a private static method.
- [X] T015 [P] [US2] Create `src/backend/FinanceTracker/Finance.Business/Validation/ICategoryValidator.cs` (interface) and `src/backend/FinanceTracker/Finance.Business/Validation/CategoryValidator.cs` (implementation). No dependencies; pure function on `CategoryCreateRequest.Name`.
- [X] T016 [US2] Create `src/backend/FinanceTracker/Finance.Api/Infrastructure/ProblemDetailsMappers.cs` (new folder `Infrastructure/`): a static class with method `ValidationProblemDetails ToValidationProblem(ValidationResult result)` that converts the per-field `Errors` into the `errors` dictionary expected by `ValidationProblemDetails`. Used by all three controllers.
- [X] T017 [US2] Extend `src/backend/FinanceTracker/Finance.Api/Controllers/TransactionsController.cs` (from US1) with `[HttpPost] Create([FromBody] TransactionCreateRequest request)` and `[HttpDelete("{id:int}")] Delete(int id)`. `Create` calls `validator.ValidateForCreate(...)` first; on invalid returns `BadRequest(ProblemDetailsMappers.ToValidationProblem(result))`; on valid uses `request.ToEntity(0)` → `repository.Add(...)` → `stored.ToResponse(categoriesById)` → `CreatedAtAction(nameof(GetById), new { id = stored.Id }, response)`. `Delete` returns `NoContent()` on `true`, otherwise `NotFound(new ProblemDetails {...})`. Inject `ITransactionValidator` via the constructor.
- [X] T018 [US2] Extend `src/backend/FinanceTracker/Finance.Api/Controllers/CategoriesController.cs` (from US1) with `[HttpPost] Create([FromBody] CategoryCreateRequest request)`. Calls `validator.ValidateForCreate(...)` first; on invalid returns 400; on valid calls `repository.Add(...)` inside a `try/catch (InvalidOperationException ex)` block. On `InvalidOperationException`, returns `Conflict(new ProblemDetails { Status = 409, Title = "Duplicate category name", Detail = ex.Message })`. On success, `CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored.ToResponse())`. Inject `ICategoryValidator`.
- [X] T019 [US2] Modify `src/backend/FinanceTracker/Finance.Api/Program.cs`: add `using Finance.Business.Validation;`, then `builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();` and `builder.Services.AddSingleton<ICategoryValidator, CategoryValidator>();` after the repository registrations.

**Checkpoint**: At this point, US1 + US2 are both fully functional. All 11 US2 tests (8 validator unit + 5 integration; T011 added 3 tests + T012 added 2) plus all 6 US1 tests pass.

---

## Phase 5: User Story 3 - Generate a period report (Priority: P3)

**Goal**: `POST /api/reports` with a `Period` envelope returns the summary `ReportResult` with the documented seed-window numbers, multi-category attribution, and income-first / expense-second / alphabetical sort. Unsupported / unknown / `IsoWeek` types → 400. Invalid `data` payload → 400. Two identical requests are byte-identical.

**Independent Test**: `POST /api/reports {type:"Period", data:{start:"2026-05-01", end:"2026-05-31"}}` returns 200 with `incomeTotal=1200.00, expenseTotal=121.29, netTotal=1078.71`, and a 5-item breakdown sorted Salary, Entertainment, Groceries, Transport, Utilities. `POST /api/reports {type:"IsoWeek", ...}` returns 400. `POST /api/reports {type:"Period", data:{start:"2026-05-31", end:"2026-05-01"}}` returns 400.

### Tests for User Story 3

> Write these tests **first**. Strategy and factory tests need their target types; integration tests need the full pipeline including DI.

- [X] T020 [P] [US3] Create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Reports/ReportStrategyFactoryTests.cs` with three `[Fact]` tests: `TryGet_returns_PeriodReportStrategy_for_Period`, `TryGet_returns_null_for_IsoWeek_when_no_strategy_registered`, `Constructor_throws_when_two_strategies_share_a_type`. The first two construct the factory with a hand-built `IEnumerable<IReportStrategy>`. Per [data-model.md §6](data-model.md#6-tests).
- [X] T021 [P] [US3] Create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Reports/PeriodReportStrategyTests.cs` with seven `[Fact]` tests: `Generates_summary_for_full_seed_window_with_documented_numbers` (SC-003), `Returns_zero_totals_and_empty_breakdown_for_empty_window`, `Includes_transaction_on_exact_start_boundary` (FR-021), `Includes_transaction_on_exact_end_boundary` (FR-021), `Multi_category_transaction_contributes_full_amount_to_each_category` (FR-025 / SC-005 — uses a custom-constructed repository or a runtime-added multi-category transaction), `Breakdown_sort_order_is_income_first_then_alphabetical` (FR-026), `Throws_ReportValidationException_when_start_greater_than_end` (FR-019).
- [X] T022 [P] [US3] Create `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs` with five `[Fact]` tests: `POST_reports_period_returns_200_with_documented_seed_window_numbers` (SC-003), `POST_reports_iso_week_returns_400_with_unsupported_type` (FR-018), `POST_reports_unknown_type_returns_400` (FR-017), `POST_reports_period_with_start_after_end_returns_400` (FR-019), `POST_reports_period_twice_returns_byte_identical_bodies` (SC-004).

### Implementation for User Story 3

- [X] T023 [P] [US3] Create `src/backend/FinanceTracker/Finance.Business/Reports/IReportStrategy.cs`: interface with `ReportType Type { get; }` property and `ReportResult Generate(ReportRequest request)` method.
- [X] T024 [P] [US3] Create `src/backend/FinanceTracker/Finance.Business/Reports/IReportStrategyFactory.cs`: interface with `IReportStrategy? TryGet(ReportType type)`.
- [X] T025 [P] [US3] Create `src/backend/FinanceTracker/Finance.Business/Reports/ReportValidationException.cs`: extends `Exception`, carries `IReadOnlyList<ValidationError> Errors`.
- [X] T026 [US3] Create `src/backend/FinanceTracker/Finance.Business/Reports/ReportStrategyFactory.cs` implementing `IReportStrategyFactory`. Constructor takes `IEnumerable<IReportStrategy> strategies` and builds `_strategies = strategies.ToDictionary(s => s.Type)`. `TryGet` is a dictionary lookup.
- [X] T027 [US3] Create `src/backend/FinanceTracker/Finance.Business/Reports/PeriodReportStrategy.cs` implementing `IReportStrategy` per [data-model.md §3](data-model.md#3-report-system-services). Pipeline: deserialize `request.Data` into `PeriodReportData` (catch `JsonException` → throw `ReportValidationException`); validate `start` / `end` non-default and `start <= end` (throw `ReportValidationException` on failure); filter transactions by `[start 00:00:00, end 23:59:59]`; compute `incomeTotal`, `expenseTotal`, `netTotal`; accumulate per-category signed totals (one entry per attached `categoryId` per transaction — multi-category attribution); sort income-first / expense-second / alphabetical-within; format `period` as `"yyyy-MM-dd..yyyy-MM-dd"`; return `ReportResult`.
- [X] T028 [US3] Create `src/backend/FinanceTracker/Finance.Api/Controllers/ReportsController.cs`. Inject `IReportStrategyFactory`. Single `[HttpPost] Generate([FromBody] ReportRequest request)` method: `factory.TryGet(request.Type)` → if `null` return `BadRequest(new ProblemDetails { Status = 400, Title = "Unsupported report type", Detail = $"Report type '{request.Type}' is not supported." })`; else `try { return Ok(strategy.Generate(request)); } catch (ReportValidationException ex) { return BadRequest(ProblemDetailsMappers.ToValidationProblem(ex.Errors)); }`.
- [X] T029 [US3] Modify `src/backend/FinanceTracker/Finance.Api/Program.cs`: add `using Finance.Business.Reports;`, then `builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();` and `builder.Services.AddSingleton<IReportStrategyFactory, ReportStrategyFactory>();` after the validator registrations.

**Checkpoint**: All three user stories are independently functional. The full test suite (US1 + US2 + US3 = ~25 new tests this feature, plus the 22 from feature 002 + 11 from feature 001 already in place) passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, manual sanity check, and the constitution Principle V agent-log entry.

- [X] T030 Run `dotnet build src/backend/FinanceTracker/FinanceTracker.slnx` (must succeed with 0 errors / 0 warnings); then run `dotnet test` against the full solution. All test projects must pass and the suite must finish in under 15 seconds (spec SC-006).
- [ ] T031 Walk the [quickstart.md sanity-check session](quickstart.md#sanity-check-session-curl-against-a-running-host) end-to-end against a live `dotnet run --project Finance.Api` host. Confirm every documented `curl` line returns the documented status code and body. Confirm the OpenAPI document at `/openapi/v1.json` lists the eight new endpoints and **does not** list `/WeatherForecast`.
- [X] T032 Append one entry to `ai-artifacts/agent_log.txt` (per constitution Principle V) covering: validation result shape ([research.md §1](research.md#1-validation-result-shape--return-based-or-exception-based)), type-compat check location ([research.md §2](research.md#2-where-the-type-compatibility-check-lives)), factory + IsoWeek handling ([research.md §3](research.md#3-factory-lookup-data-structure-and-how-isoweek-without-a-strategy-is-handled)), `ReportValidationException` narrow scope ([research.md §4](research.md#4-where-start--end-and-date-shape-validation-live-in-the-strategy-not-in-the-controller)), `Microsoft.AspNetCore.Mvc.Testing` package addition ([research.md §7](research.md#7-integration-test-infrastructure-choice)), and WeatherForecast scaffold removal ([research.md §8](research.md#8-removing-the-default-weatherforecastcontroller-scaffold)). Include timestamp, model/tool, prompt summary, decision (accepted/rejected), and reason for each.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on T003 (the csproj must be restorable before `ApiTestFixture` can compile against `WebApplicationFactory<Program>`).
- **US1 (Phase 3)**: Depends on T004 (`ApiTestFixture` needed by US1 integration tests).
- **US2 (Phase 4)**: Depends on US1 only because the integration tests extend US1's test files (same files, different `[Fact]`s). Validators have no dependency on US1.
- **US3 (Phase 5)**: Depends on US2 (its `ReportValidationException` uses `ValidationError` from US2's T013). The report integration tests extend `ApiTestFixture` from Foundational.
- **Polish (Phase 6)**: Depends on US1 + US2 + US3.

### User Story Dependencies

- **US1 (P1)** is independent — only needs ApiTestFixture and the seeded repos from feature 002.
- **US2 (P2)** depends on:
  - US1 file (`TransactionsController.cs`, `CategoriesController.cs`) being created (extends them).
  - US1 file (`TransactionsEndpointsTests.cs`, `CategoriesEndpointsTests.cs`) being created (extends them).
  - Foundational (`ApiTestFixture.cs`).
- **US3 (P3)** depends on:
  - US2 (`ValidationError.cs`, `ValidationResult.cs` — used by `ReportValidationException` and the controller's problem-details mapping).
  - Foundational (`ApiTestFixture.cs`).

### Within Each User Story

- Tests are written **before** implementation tasks within each phase (the test tasks have lower IDs than their corresponding implementation tasks).
- For US1, tests fail with 404 (no controller exists) until T007/T008 land.
- For US2, validator tests fail to compile until T013–T015 land; integration tests fail with 404 until T017–T019 land.
- For US3, strategy + factory unit tests fail to compile until T023–T027 land; integration tests fail with 404 until T028–T029 land.

### Parallel Opportunities

- **Setup**: T001, T002, T003 are independent — all three can run in parallel by file but the task IDs are sequential because they're small and intertwined enough that running them in order is fine.
- **Foundational**: only T004; nothing to parallelize.
- **Within US1**: T005 ∥ T006 (separate test files); T007 ∥ T008 (separate controller files).
- **Within US2**:
  - All four test files extend / create distinct files → T009 ∥ T010 ∥ T011 ∥ T012.
  - T013 (validation types) and T014 (TransactionValidator) and T015 (CategoryValidator) touch separate files → all three ∥.
  - T016 (ProblemDetailsMappers) is independent of validators → ∥ with T013/T014/T015.
  - T017 and T018 both edit Program.cs adjacents but T017 edits `TransactionsController.cs` and T018 edits `CategoriesController.cs` — different files → ∥.
  - T019 edits `Program.cs` and depends on T017/T018 having finished (so the validator types exist in the DI graph) — sequential.
- **Within US3**:
  - T020 ∥ T021 ∥ T022 (three separate test files).
  - T023 ∥ T024 ∥ T025 (three separate interface / exception files).
  - T026 depends on T023+T024 — sequential.
  - T027 depends on T023+T025 — sequential, but T027 ∥ T026 OK because the dictionary just keys by `Type`.
  - T028 depends on T027 + (a registered factory) — wait, T028 only depends on `IReportStrategyFactory` interface, not the impl. So T028 ∥ T026 OK.
  - T029 depends on T026 + T027 (registers them both) — sequential.

---

## Parallel Example: User Story 1

```text
# After T001..T004 complete:

# Two developers split the work:
Developer A: T005 (TransactionsEndpointsTests US1 tests) + T007 (TransactionsController GET)
Developer B: T006 (CategoriesEndpointsTests US1 tests)   + T008 (CategoriesController GET)

# Each writes their test file first, watches it fail with 404,
# then adds their controller and watches it pass.
```

## Parallel Example: User Story 2

```text
# After US1 complete:

# Four developers (extreme parallel) — write all tests first, in parallel:
Developer A: T009 (TransactionValidatorTests)
Developer B: T010 (CategoryValidatorTests)
Developer C: T011 (Transactions integration test extensions)
Developer D: T012 (Categories integration test extensions)

# Then three developers split the production code in parallel:
Developer A: T013 (ValidationError + ValidationResult) -> T014 (TransactionValidator)
Developer B: T015 (CategoryValidator)
Developer C: T016 (ProblemDetailsMappers)

# Then two developers split the controller extensions in parallel:
Developer A: T017 (TransactionsController POST + DELETE)
Developer B: T018 (CategoriesController POST)

# Finally one developer runs:
T019 (Program.cs DI registrations)
```

---

## Implementation Strategy

### MVP first (US1 only)

1. Complete Setup (T001–T003).
2. Complete Foundational (T004).
3. Complete US1 (T005–T008).
4. **STOP and VALIDATE**: run integration tests; confirm `dotnet run --project Finance.Api` + `curl https://localhost:7266/api/transactions` returns the seed list.
5. The MVP at this point: the seeded data is reachable over HTTP. Reads work. Writes and reports follow.

### Incremental delivery

1. MVP (US1) → ship internally; consumers can already build a read-only client.
2. Add US2 → consumers can now create / delete records with structured validation feedback.
3. Add US3 → reports are available; the feature is feature-complete.
4. Polish (Phase 6) → SC-006 timing + manual quickstart + agent log.

### Single-developer realistic ordering

Given this feature is significantly larger than 001 and 002 (~600 lines production + ~700 lines tests), a realistic single-developer ordering is:

1. T001 + T002 + T003 (folder + scaffold removal + csproj edit; ~5 minutes).
2. T004 (ApiTestFixture; ~10 minutes).
3. T005 + T006 + T007 + T008 (US1 — write tests first, then controllers; ~30 minutes).
4. T009..T012 + T013..T016 + T017..T019 (US2 — write tests first per file pair, then implementations, then DI; ~90 minutes).
5. T020..T022 + T023..T029 (US3 — write tests first, then interfaces + types, then factory + strategy + controller, then DI; ~75 minutes).
6. T030 + T031 + T032 (Polish; ~30 minutes).

Single-developer end-to-end estimate: **4–5 hours**.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- This feature is the first to add a new third-party NuGet package (`Microsoft.AspNetCore.Mvc.Testing`) — see [research.md §7](research.md#7-integration-test-infrastructure-choice) for the justification.
- The `Finance.Api.UnitTests` project remains intentionally empty after this feature; controller behavior is more usefully verified at the integration-test level.
- The default `WeatherForecastController.cs` and `WeatherForecast.cs` scaffold files are deleted in T002. They have never been part of the product surface.
- Commit after each phase or each [P] cluster within a phase. The optional `after_tasks` / `after_implement` git extension hooks will offer commits automatically.
- The implementation strategy intentionally treats US1 as the "controller file is born" story and US2 as the "controller file gains write methods" story. This is the realistic shape for incremental development and lines up with the same pattern feature 002 used for its repository files.
