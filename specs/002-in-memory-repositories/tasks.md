---

description: "Task list for feature 002-in-memory-repositories"
---

# Tasks: Seeded In-Memory Repositories

**Input**: Design documents from `/specs/002-in-memory-repositories/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/repository-contracts.md](contracts/repository-contracts.md), [quickstart.md](quickstart.md)

**Tests**: REQUIRED. Constitution Principle IV (Test-First with xUnit v3) mandates the Principle IV "Repository behaviour (singleton lifetime, seeded data shape, integer-ID determinism)" coverage bullet, which is explicitly assigned to this feature in [plan.md §Constitution Check](plan.md#constitution-check). All tests use `Xunit.Assert` only — no FluentAssertions (constitution Principle IV).

**Organization**: Tasks are grouped by user story (US1, US2, US3 from [spec.md](spec.md)) to enable independent verification of each story. Because the production code surface is small (~150 lines across two repository files), the full implementation lands in US1; later stories add only their tests + the DI wiring step that depends on the implementations existing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are absolute from repo root

## Path Conventions

This feature is a **web service** (ASP.NET Core Web API). All paths are relative to repo root, rooted under `src/backend/FinanceTracker/` for source and `src/backend/FinanceTracker/tests/` for tests, per [plan.md §Project Structure](plan.md#project-structure).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure.

Solution, csprojs, project references, and entity types from feature 001 already exist on disk. This feature introduces no new NuGet packages and no new csproj edits. The only Setup-phase work is creating the empty target folder and confirming preconditions.

- [X] T001 Create the directory `src/backend/FinanceTracker/Finance.Data/Repositories/` (folder only — no files yet). Verify that `src/backend/FinanceTracker/Finance.Data/Finance.Data.csproj` and `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Finance.Data.UnitTests.csproj` already exist and that the test project's csproj already contains `<ProjectReference Include="..\..\Finance.Data\Finance.Data.csproj" />`. No edits required if both conditions hold.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Repository interface declarations. Every user story below depends on these — they define the contract that the in-memory implementations satisfy and that future controller / business-layer features will inject.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create `src/backend/FinanceTracker/Finance.Data/Repositories/ITransactionRepository.cs` declaring `namespace Finance.Data.Repositories;` and the four-member interface `IReadOnlyCollection<Transaction> GetAll()`, `Transaction? GetById(int id)`, `Transaction Add(Transaction transaction)`, `bool Delete(int id)` exactly as in [contracts/repository-contracts.md §ITransactionRepository](contracts/repository-contracts.md#itransactionrepository). Include `using Finance.Data.Models;`.
- [X] T003 [P] Create `src/backend/FinanceTracker/Finance.Data/Repositories/ICategoryRepository.cs` declaring `namespace Finance.Data.Repositories;` and the three-member interface `IReadOnlyCollection<Category> GetAll()`, `Category? GetById(int id)`, `Category Add(Category category)` exactly as in [contracts/repository-contracts.md §ICategoryRepository](contracts/repository-contracts.md#icategoryrepository). Include `using Finance.Data.Models;`. **No `Delete` method** (categories are append-only per spec clarification).

**Checkpoint**: Foundation ready. The interfaces compile and can be referenced by tests in subsequent stories. User story implementation can now begin.

---

## Phase 3: User Story 1 - Demo data available immediately on app start (Priority: P1) 🎯 MVP

**Goal**: Resolving the repository layer on a fresh process yields the five seeded categories and the five seeded transactions, with deterministic literal ids, in insertion order. This is the load-bearing P1 slice — every downstream feature consumes it.

**Independent Test**: Construct `new InMemoryTransactionRepository()` and `new InMemoryCategoryRepository()` directly (no DI host needed). Call `.GetAll()` on each and assert the documented seed contents in id order. Assert `GetById(N)` for each literal seed id returns the corresponding seed record.

> **Implementation note**: The production code added in this phase implements **the full interface contract for both repositories** (constructor + seed + `GetAll` + `GetById` + `Add` + `Delete` for transactions; constructor + seed + `GetAll` + `GetById` + `Add` for categories). All three user stories share the same two implementation files, and US1 is where they land. US2 and US3 then add only their behavioral tests on top of the same code — those stories validate behavior that this phase's implementation already supports.

### Tests for User Story 1

> Write these tests **first**; they must fail (or fail to compile) before the implementation tasks below land.

- [X] T004 [P] [US1] Create test file `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryCategoryRepositoryTests.cs` with two `[Fact]` tests:
  - `GetAll_returns_five_seeded_categories_in_id_order` — asserts `GetAll().Count == 5` and that ids 1..5 map to names `Salary`, `Groceries`, `Transport`, `Entertainment`, `Utilities` with the documented `CategoryType` for each (per [data-model.md §3](data-model.md#3-seed-data--canonical-reference)).
  - `GetById_returns_seeded_category_by_literal_id` — asserts `GetById(1)!.Name == "Salary"` and `GetById(5)!.Type == CategoryType.Expense`.
- [X] T005 [P] [US1] Create test file `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryTransactionRepositoryTests.cs` with three `[Fact]` tests:
  - `GetAll_returns_five_seeded_transactions_in_id_order` — asserts `GetAll().Count == 5` and that transaction id 1 is `"Monthly salary"`/`Income`/`1200.00m` on `2026-05-01 12:00:00` with `CategoryIds = [1]`, and the other four match the seed table exactly.
  - `GetById_returns_seeded_transaction_by_literal_id` — asserts `GetById(5)!.Description == "Electricity Bill"` and `GetById(5)!.Timestamp == new DateTime(2026, 5, 10, 12, 0, 0)`.
  - `Seeded_transactions_reference_seeded_categories_only` — for every transaction returned by `GetAll()`, every id in its `CategoryIds` is present in `new InMemoryCategoryRepository().GetAll().Select(c => c.Id)`. (Maps to spec FR-002.)

### Implementation for User Story 1

- [X] T006 [P] [US1] Create `src/backend/FinanceTracker/Finance.Data/Repositories/InMemoryCategoryRepository.cs` implementing `ICategoryRepository`. Constructor seeds the list with categories 1–5 per [data-model.md §2](data-model.md#2-in-memory-implementations); `_nextId` initialized to `6`. `GetAll` returns `_categories.AsReadOnly()`; `GetById` returns `_categories.FirstOrDefault(c => c.Id == id)`. **Implement `Add` here too** (full duplicate-name-rejection logic per data-model.md) — its behavior will be validated by US2's tests in the next phase.
- [X] T007 [P] [US1] Create `src/backend/FinanceTracker/Finance.Data/Repositories/InMemoryTransactionRepository.cs` implementing `ITransactionRepository`. Constructor seeds the list with transactions 1–5 per [data-model.md §2](data-model.md#2-in-memory-implementations) — note category-id arrays use `new[] { N }` literals; `_nextId` initialized to `6`. `GetAll` returns `_transactions.AsReadOnly()`; `GetById` returns `_transactions.FirstOrDefault(t => t.Id == id)`. **Implement `Add` and `Delete` here too** (full behavior per data-model.md) — their behavior will be validated by US2 / US3 tests in subsequent phases.

**Checkpoint**: At this point, US1 should be fully functional and testable. The five US1 tests above must all pass. The class also compiles and exposes the full interface contract, but the `Add`-/`Delete`-flavored behaviors aren't yet covered by tests — that comes in US2 / US3.

---

## Phase 4: User Story 2 - Save new transactions and categories within an app lifetime (Priority: P2)

**Goal**: A consumer can add a transaction or category at runtime and see it on subsequent reads from the same process. Duplicate category names (case-insensitive) are rejected without consuming an identifier. Two resolutions of `ITransactionRepository` from the same DI provider return the same instance (singleton lifetime).

**Independent Test**: After constructing the repositories, call `Add(...)` with valid input, assert the returned record has `Id == 6`, then `GetAll()` includes it; attempt a duplicate-name category `Add` and assert it throws `InvalidOperationException` and the next legitimate `Add` still gets id `7` (no hole). For the DI test, build an `IServiceCollection`, call `AddSingleton<...>` per [data-model.md §4](data-model.md#4-dependency-injection), build the provider, resolve `ITransactionRepository` twice, and assert `ReferenceEquals`.

> **DI prerequisite**: This story requires `Finance.Api/Program.cs` to register the repositories. The DI registration task lives here (T011) because the US2 lifetime test in T010 depends on it; US1 was able to construct the repositories directly without going through DI.

### Tests for User Story 2

- [X] T008 [P] [US2] Extend `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryCategoryRepositoryTests.cs` with two more `[Fact]` tests:
  - `Add_appends_new_category_and_assigns_next_id` — construct a fresh repo, call `Add(new Category(0, "Savings", CategoryType.Income))`, assert returned `Id == 6` and `GetAll().Count == 6` and the new record is at the end of the collection.
  - `Add_rejects_duplicate_name_case_insensitively_without_consuming_id` — call `Add(new Category(0, "GROCERIES", CategoryType.Expense))` and assert `Assert.Throws<InvalidOperationException>(...)`. Then call `Add(new Category(0, "Brand-new", CategoryType.Expense))` and assert returned `Id == 6` (NOT 7 — the rejected add did not consume an id, per spec SC-005 / FR-007).
- [X] T009 [P] [US2] Extend `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryTransactionRepositoryTests.cs` with one more `[Fact]` test:
  - `Add_appends_new_transaction_and_assigns_id_six` — construct a fresh repo, call `Add(new Transaction(0, new DateTime(2026, 5, 31, 9, 0, 0), "Bonus", 250m, TransactionType.Income, new[] { 1 }))`, assert returned `Id == 6` and `GetAll().Count == 6` and `GetById(6)!.Description == "Bonus"`. (The repository does NOT validate that category id `1` is compatible with `Income` — that's a Business-layer concern per [research.md §6](research.md#6-where-validation-lives).)
- [X] T010 [P] [US2] Create test file `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/RepositoryLifetimeTests.cs` with one `[Fact]` test:
  - `Resolving_repository_twice_from_same_provider_yields_same_instance` — build a `ServiceCollection`, call the two `AddSingleton<...>` lines from [data-model.md §4](data-model.md#4-dependency-injection), `BuildServiceProvider()`, resolve `ITransactionRepository` twice and `ICategoryRepository` twice, assert `Assert.Same(first, second)` for both. (Maps to spec FR-008 and constitution Principle III singleton lifetime.)

### Implementation / Wiring for User Story 2

- [X] T011 [US2] Modify `src/backend/FinanceTracker/Finance.Api/Program.cs`: add `using Finance.Data.Repositories;` near the top and the two lines `builder.Services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();` / `builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();` after `builder.Services.AddControllers();`. Do NOT touch any other line of `Program.cs` — the OpenAPI / Scalar dev-only gating remains intact.

**Checkpoint**: At this point, US1 AND US2 are both verified. The tests for both stories must pass. The host can be started (`dotnet run --project Finance.Api`) and the DI container resolves both repositories with singleton lifetime.

---

## Phase 5: User Story 3 - Look up and remove transactions by identifier (Priority: P3)

**Goal**: `GetById` returns `null` for non-existent ids on both repositories. `Delete` on `ITransactionRepository` returns `true` after removing an existing record and `false` after a miss, never throwing.

**Independent Test**: Construct a transaction repository, `Delete(99999)` returns `false`; `Add(...)` a record then `Delete(returnedId)` returns `true` and `GetById(returnedId)` returns `null`; a second `Delete(returnedId)` returns `false` without throwing.

### Tests for User Story 3

- [X] T012 [P] [US3] Extend `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryCategoryRepositoryTests.cs` with one more `[Fact]` test:
  - `GetById_returns_null_for_unknown_id` — assert `GetById(99999)` and `GetById(0)` both return `null` and do not throw.
- [X] T013 [P] [US3] Extend `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryTransactionRepositoryTests.cs` with three more `[Fact]` tests:
  - `GetById_returns_null_for_unknown_id` — `GetById(99999)` and `GetById(0)` both return `null`.
  - `Delete_existing_returns_true_and_removes_record` — pick a seeded id (e.g., `2`), assert `Delete(2) == true`, assert `GetById(2) == null`, assert `GetAll().Count == 4`.
  - `Delete_missing_returns_false_and_does_not_throw` — assert `Delete(99999) == false`. Then `Delete(2)` (the seeded record from the previous fact would be a shared-state hazard if these tests shared a fixture, so this fact MUST construct its own fresh repository).

**Checkpoint**: All three user stories are independently verified. The complete test list (the eleven xUnit `[Fact]` tests above) passes against the implementation from US1 + the DI wiring from US2.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cover the success-criteria perf smoke test, the agent-log obligation, and the manual quickstart sanity check that proves the demo path works end-to-end.

- [X] T014 [P] Add one `[Fact]` test to `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/InMemoryTransactionRepositoryTests.cs`:
  - `Adds_one_hundred_transactions_with_reads_under_fifty_milliseconds` — construct a fresh repo, loop `for (int i = 0; i < 100; i++) repo.Add(...)`, then run a sample of `GetById` and `GetAll` calls within a `Stopwatch`. Assert total elapsed time is well under one second (the 50 ms-per-read SC-004 bar is by design easy to clear — this test exists to catch a future change that accidentally introduces O(n²) behavior, not to measure latency precisely). (Maps to spec SC-004.)
- [X] T015 Run `dotnet build` then `dotnet test --filter "FullyQualifiedName~Finance.Data.UnitTests.Repositories"` from `src/backend/FinanceTracker/`. All twelve tests must pass and the suite must finish in under 5 seconds (spec SC-002).
- [X] T016 Walk the [quickstart.md "Verification checklist"](quickstart.md#verification-checklist-after-implementation) end-to-end and tick every box. Confirms the file layout, the singleton registrations, and the no-FluentAssertions invariant from a fresh-eyes pass.
- [X] T017 Append one entry to `ai-artifacts/agent_log.txt` covering the meaningful AI-assisted decisions in this feature (per constitution Principle V) — at minimum: the seed time-of-day choice ([research.md §1](research.md#1-time-of-day-component-of-seeded-transaction-timestamps)), the id-generator decision ([research.md §2](research.md#2-identifier-generation-strategy)), the no-locking concurrency stance ([research.md §4](research.md#4-concurrency-stance)), and the validation-boundary choice ([research.md §6](research.md#6-where-validation-lives)). Include timestamp, model/tool, prompt summary, decision (accepted/rejected), and reason for each.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001 must complete so the `Repositories/` folder exists). Blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion (T002, T003 both done).
- **User Story 2 (Phase 4)**: Depends on US1 completion. T011 (DI wiring) requires T006 + T007 (the implementation classes) to exist; T010 (the DI lifetime test) requires T011.
- **User Story 3 (Phase 5)**: Depends on US1 completion (the implementation files exist; US3 tests run against them).
- **Polish (Phase 6)**: Depends on US1 + US2 + US3 — T014 / T015 / T016 verify the full feature; T017 logs the decisions made across all phases.

### User Story Dependencies

- **US1 (P1)** is the implementation-bearing story. The two `InMemory*Repository.cs` files are created here in full, including the `Add` / `Delete` methods that US2 / US3 exercise. This is a deliberate scope choice for a ~150-line feature: see [Implementation Strategy](#implementation-strategy) below.
- **US2 (P2)** depends on US1 because its tests run against the `Add` method that US1 implemented. T011 (DI wiring in `Program.cs`) is the only production-code change in US2.
- **US3 (P3)** depends on US1 because its tests run against the `Delete` and `GetById` methods that US1 implemented. **US3 adds no production code.**

### Within Each User Story

- Tests are written first (the [P]-marked test tasks at the top of each story phase). They MUST fail before the implementation tasks land.
- For US1, "fail" means "won't compile because the `InMemory*Repository` types do not exist yet". Run the test project; expect compile errors. Then T006 + T007 make them compile and pass.
- For US2 / US3, "fail" means the assertion fails when run against the US1 implementation. Since US1 implemented the methods in full, US2 / US3 tests should actually **pass on the first run** — confirming the US1 implementation is correct. The TDD spirit (write the test, see it fail, then make it pass) is honored at the seam between Foundational and US1; US2 / US3 are verification slices.

### Parallel Opportunities

- T002 (`ITransactionRepository.cs`) and T003 (`ICategoryRepository.cs`) are separate files → [P].
- Within US1: T004 (category tests file) and T005 (transaction tests file) are separate files → [P]. T006 (category impl) and T007 (transaction impl) are separate files → [P]. Tests and implementation within a story should be done in TDD order (tests first → fail → impl → pass), but the two test files and the two implementation files can each be split across two developers.
- Within US2: T008, T009, T010 are three separate test files / file-extensions → all [P] with each other. T011 (`Program.cs`) is a separate file and only depends on the implementations existing → it can run in parallel with T008/T009/T010 once T006/T007 are done.
- Within US3: T012 (category test extension) and T013 (transaction test extension) are separate files → [P].
- US2 and US3 are independent of each other in terms of files touched (US2 edits `Program.cs` + the category/transaction tests with `Add`-flavored facts; US3 edits the category/transaction tests with `GetById`-miss / `Delete` facts) — but they edit the **same two test files** with different `[Fact]` methods, so they are NOT [P] across stories. Sequential between US2 and US3 is required.

---

## Parallel Example: User Story 1

```text
# After T002, T003 complete (interfaces exist):

# Two developers split the work — one per repository:
Developer A: T004 (category tests file) + T006 (category implementation)
Developer B: T005 (transaction tests file) + T007 (transaction implementation)

# Within each developer's strand: write the test file first, watch it
# fail to compile, then add the implementation file and watch it pass.
```

---

## Implementation Strategy

### MVP first (US1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (both interfaces exist).
3. Complete Phase 3: US1.
4. **STOP and VALIDATE**: run `dotnet test --filter "FullyQualifiedName~InMemoryCategoryRepositoryTests|FullyQualifiedName~InMemoryTransactionRepositoryTests"` — the five US1 tests should pass. The `Add` / `Delete` methods are already implemented at this point but are uncovered by tests; that's fine for the MVP slice (the spec's P1 is "demo data on start", not "writes").
5. The MVP at this point: a developer who clones the repo and runs `dotnet run --project Finance.Api` can construct the repositories programmatically and see the five seed transactions / five seed categories. HTTP endpoints to read them out over the wire are still a separate feature (003+).

### Incremental delivery

1. MVP (US1) → ship internally; reports / controllers features can start consuming the interfaces immediately.
2. Add US2 → wires up DI so future controllers don't need to know about implementation classes, and adds tests that pin down the Add behavior including the case-insensitive duplicate-name rule.
3. Add US3 → adds tests that pin down the `GetById`-miss and `Delete` semantics. No production-code change.
4. Polish (Phase 6) → SC-004 perf smoke + manual quickstart + agent log.

### Single-developer realistic ordering

Given this is a ~150-line feature, a realistic single-developer ordering is:

1. T001 (folder)
2. T002 + T003 (interfaces — can be written in one editor sitting)
3. T004 + T005 + T006 + T007 (US1 — write test files first per TDD, then the implementations; the back-and-forth between the four files takes maybe 30 minutes)
4. T008 + T009 + T010 + T011 (US2 — write the three test additions, then add the DI registration to `Program.cs`)
5. T012 + T013 (US3 — extend the two test files; no production code)
6. T014 + T015 + T016 + T017 (Polish)

Single-developer end-to-end estimate: 1.5–2 hours.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- The "implementation is monolithic, tests are sliced per story" framing is documented explicitly in US1 → US2 → US3 because this feature's production code is too small to slice meaningfully by story. The conventional "implement only what US1 needs, then add Add for US2, then add Delete for US3" approach would force NotImplementedException stubs in US1's classes (since C# interface implementations must cover all members) — a code smell that nobody asked for. See [Within Each User Story](#within-each-user-story) for how TDD is preserved despite this.
- The feature deliberately leaves business validation (`Amount > 0`, category referential integrity, multi-category compatibility with `TransactionType`) out of scope. See [spec.md §Assumptions](spec.md#assumptions) and [research.md §6](research.md#6-where-validation-lives). A future feature (transactions / categories HTTP endpoints + business-layer validators) will own those rules; the contract this feature delivers is stable across that future addition.
- No NuGet packages are added by this feature. The test project already has `xunit.v3` from feature 001. Implementation uses BCL types only.
- Commit after each phase or each [P] cluster within a phase. The optional `after_tasks` / `after_implement` git extension hooks will offer commits automatically.
