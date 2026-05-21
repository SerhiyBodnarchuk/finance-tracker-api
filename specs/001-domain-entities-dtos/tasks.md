---
description: "Task list for feature 001-domain-entities-dtos"
---

# Tasks: Domain Entities and API DTOs

**Feature**: `001-domain-entities-dtos`
**Branch**: `001-domain-entities-dtos`
**Input**: Design documents under [specs/001-domain-entities-dtos/](.)
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: ✅ **Included.** The user input explicitly requested tests ("Tests for aggregation logic and edge cases for overlapping week boundaries; integration test for end-to-end transaction creation and reporting") and constitution v2.0.1 Principle IV requires test coverage for entities, DTOs, mappers, and JSON round-trip fidelity. The test file inventory comes from [quickstart.md §6](quickstart.md).

**Organization**: Tasks are grouped by user story (US1–US6 from [spec.md](spec.md)). User-story phases run in priority order (P1 → P2 → P3); within a story, [P] tasks operate on disjoint files and can run in parallel.

> **Revision 2026-05-21**: MVP scoped down to **Period report type only**. `IsoWeekReportData` (DTO), the IsoWeek branch in [contracts/report-request.schema.json](contracts/report-request.schema.json), and the US6 IsoWeek payload-binding test case are deferred to the future IsoWeek strategy feature. `ReportType.IsoWeek` stays in the foundational enum (T006) as a documented future value so the README's `ReportResult` example continues to serve as the US5 test fixture. Net effect: one task dropped (was 31, now 30); US6 reduced from 4 to 3 tasks.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Different file, no dependency on an incomplete task — parallelizable.
- **[Story]**: User-story label (US1–US6). Setup, Foundational, and Polish phases carry no story label.
- All file paths are relative to the **repository root** (`c:\Projects\Currys\finance-tracker-api\`).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire project references so production projects can see the layer below and test projects can see the projects under test. No source files yet.

- [X] T001 Add `<ProjectReference Include="..\Finance.Data\Finance.Data.csproj" />` to `src/backend/FinanceTracker/Finance.Business/Finance.Business.csproj`
- [X] T002 [P] Add `<ProjectReference Include="..\..\Finance.Data\Finance.Data.csproj" />` to `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Finance.Data.UnitTests.csproj`
- [X] T003 [P] Add `<ProjectReference>` entries for `Finance.Business` and `Finance.Data` to `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Finance.Business.UnitTests.csproj`

**Checkpoint**: `dotnet restore` succeeds; `dotnet build` succeeds against the still-empty projects (csproj-only build).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Three enums plus the centralized `JsonSerializerOptions` instance. These are referenced by entities, DTOs, mappers, and every round-trip test in later phases.

**⚠️ CRITICAL**: No user-story phase can compile until this phase is complete.

- [X] T004 [P] Create `TransactionType` enum (`Income`, `Expense`) at `src/backend/FinanceTracker/Finance.Data/Models/TransactionType.cs` (namespace `Finance.Data.Models`) per [data-model.md](data-model.md#enums)
- [X] T005 [P] Create `CategoryType` enum (`Income`, `Expense`, `Both`) at `src/backend/FinanceTracker/Finance.Data/Models/CategoryType.cs` (namespace `Finance.Data.Models`) per [data-model.md](data-model.md#enums)
- [X] T006 [P] Create `ReportType` enum (`Period`, `IsoWeek`) at `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs` (namespace `Finance.Business.Enums`) per [data-model.md](data-model.md#enum)
- [X] T007 [P] Create `JsonSerializationOptions` static class exposing `JsonSerializerOptions Default` at `src/backend/FinanceTracker/Finance.Business/JsonSerializationOptions.cs` per [research.md §2](research.md#2-json-serialization-configuration-for-enums-dates-and-decimals) (camelCase naming policy; `JsonStringEnumConverter` with **no** naming policy so enum values stay declared-case; `DefaultIgnoreCondition = Never`)

**Checkpoint**: All four enum/options files compile. `dotnet build` clean. User stories can now begin.

---

## Phase 3: User Story 1 — Represent a transaction with one-or-more categories (Priority: P1) 🎯 MVP

**Goal**: Define the `Transaction` domain record so any subsequent feature can describe a single income/expense entry with `int Id`, full second-precision `DateTime`, description, unsigned `decimal` amount, direction, and a non-empty `IReadOnlyList<int>` of category references.

**Independent Test**: Construct a `Transaction` value with all fields populated (including the multi-category case); confirm every field is exposed as supplied with no precision loss; confirm two transactions with the same data but different `Id`s are distinct under record equality; confirm `CategoryIds` is exposed as `IReadOnlyList<int>` and preserves insertion order.

### Implementation for User Story 1

- [X] T008 [US1] Create `public sealed record Transaction(int Id, DateTime Timestamp, string Description, decimal Amount, TransactionType Type, IReadOnlyList<int> CategoryIds)` at `src/backend/FinanceTracker/Finance.Data/Models/Transaction.cs` (namespace `Finance.Data.Models`) per [data-model.md §Transaction](data-model.md#transaction). Implements FR-001, FR-002, FR-008.

### Tests for User Story 1

- [X] T009 [US1] Create `TransactionTests.cs` at `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/TransactionTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) construct with all fields → each field round-trips by value, (b) two transactions with same data + different `Id` → `Assert.NotEqual`, (c) `Assert.IsAssignableFrom<IReadOnlyList<int>>(transaction.CategoryIds)`, (d) multi-category transaction preserves `CategoryIds` order. Use `Xunit.Assert` only — **no FluentAssertions**.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TransactionTests"` runs green. User Story 1 is independently complete.

---

## Phase 4: User Story 2 — Represent a category with name and supported direction (Priority: P1)

**Goal**: Define the `Category` domain record (`int Id`, `string Name`, `CategoryType Type`) and its create-request DTO `CategoryCreateRequest` so categories can be described and created without an identifier-leaking shape.

**Independent Test**: Construct a `Category` with each of the three `CategoryType` values; confirm each field is exposed as supplied; confirm two `Category` values whose `Name`s differ only by case are equivalent under `StringComparer.OrdinalIgnoreCase` (the record stores the name as-supplied — case-insensitive equivalence is the consumer's check per FR-007).

### Implementation for User Story 2

- [X] T010 [P] [US2] Create `public sealed record Category(int Id, string Name, CategoryType Type)` at `src/backend/FinanceTracker/Finance.Data/Models/Category.cs` (namespace `Finance.Data.Models`) per [data-model.md §Category](data-model.md#category). Implements FR-003, FR-007 (storage), FR-008.
- [X] T011 [P] [US2] Create `public sealed record CategoryCreateRequest(string Name, CategoryType Type)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Categories/CategoryCreateRequest.cs` (namespace `Finance.Business.Dtos.Categories`) per [data-model.md §DTOs — Categories](data-model.md#dtos--categories). Implements FR-010, FR-014.

### Tests for User Story 2

- [X] T012 [US2] Create `CategoryTests.cs` at `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/CategoryTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) construct with each of the three `CategoryType` values → each field round-trips, (b) two `Category` values with `Name = "Groceries"` vs `"groceries"` compare equal under `StringComparer.OrdinalIgnoreCase`. Use `Xunit.Assert` only.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~CategoryTests"` runs green. User Story 2 is independently complete.

---

## Phase 5: User Story 3 — Submit a new transaction through a stable API request shape (Priority: P1)

**Goal**: Define `TransactionCreateRequest` — the consumer-facing DTO that carries `Timestamp`, `Description`, `Amount`, `Type`, and `CategoryIds`, but **no `Id`** (server assigns it).

**Independent Test**: Bind the README's example payload (`{ "timestamp": "2026-05-07T09:15:00", "description": "Coffee", "amount": 4.50, "transactionType": "Expense", "categoryIds": [3] }`) into `TransactionCreateRequest`; confirm every field is captured with the expected name and type; confirm immutability (record positional init); confirm decimal precision is preserved on the wire.

### Implementation for User Story 3

- [X] T013 [US3] Create `public sealed record TransactionCreateRequest(DateTime Timestamp, string Description, decimal Amount, TransactionType Type, IReadOnlyList<int> CategoryIds)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Transactions/TransactionCreateRequest.cs` (namespace `Finance.Business.Dtos.Transactions`) per [data-model.md §DTOs — Transactions](data-model.md#dtos--transactions). Implements FR-009, FR-014.

**Note on US3 test coverage**: The README's example payload is round-tripped through this DTO inside `TransactionMapperTests` (T021), which constructs a `TransactionCreateRequest` from the same payload before exercising `ToEntity`. No separate `TransactionCreateRequestRoundTripTests` file is added — the wire-shape coverage is owned by the mapper test per the team's chosen test inventory in [quickstart.md §6](quickstart.md#6-add-the-tests).

**Checkpoint**: `dotnet build` clean. User Story 3 is independently complete (test coverage shared with US4's mapper tests).

---

## Phase 6: User Story 4 — Receive transaction and category data in a stable, predictable response shape (Priority: P2)

**Goal**: Define the response DTOs (`TransactionResponse`, `CategoryResponse`, `CategorySummary`) and the two entity ↔ DTO mappers (`TransactionMapper`, `CategoryMapper`) so the API layer can return well-shaped data without ever seeing a domain entity.

**Independent Test**: Construct a `TransactionResponse` from a `Transaction` tagged with two categories (matching the README's example) via `TransactionMapper.ToResponse`; serialize with `JsonSerializationOptions.Default`; confirm the produced JSON matches the README's example field-for-field (including nested `categories` collection order, camelCase property names, signed `amount`, and `transactionType` rendered as `"Expense"`); deserialize back and confirm structural equality with the original.

### Implementation for User Story 4

- [X] T014 [P] [US4] Create `public sealed record CategorySummary(int Id, string Name)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Transactions/CategorySummary.cs` (namespace `Finance.Business.Dtos.Transactions`) per [data-model.md §DTOs — Transactions](data-model.md#dtos--transactions). Implements FR-015 (nested item).
- [X] T015 [P] [US4] Create `public sealed record TransactionResponse(int Id, DateTime Timestamp, string Description, decimal Amount, TransactionType Type, IReadOnlyList<CategorySummary> Categories)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Transactions/TransactionResponse.cs` (namespace `Finance.Business.Dtos.Transactions`) per [data-model.md §DTOs — Transactions](data-model.md#dtos--transactions). Implements FR-015, FR-017.
- [X] T016 [P] [US4] Create `public sealed record CategoryResponse(int Id, string Name, CategoryType Type)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Categories/CategoryResponse.cs` (namespace `Finance.Business.Dtos.Categories`) per [data-model.md §DTOs — Categories](data-model.md#dtos--categories). Implements FR-016, FR-017.
- [X] T017 [P] [US4] Create `public static class CategoryMapper` with extension methods `ToResponse(this Category)` and `ToEntity(this CategoryCreateRequest, int assignedId)` at `src/backend/FinanceTracker/Finance.Business/Mappers/CategoryMapper.cs` (namespace `Finance.Business.Mappers`) per [data-model.md §Mappers](data-model.md#mappers). Implements FR-023, FR-024.
- [X] T018 [P] [US4] Create `public static class TransactionMapper` with extension methods `ToResponse(this Transaction, IReadOnlyDictionary<int, Category> categoriesById)` and `ToEntity(this TransactionCreateRequest, int assignedId)` at `src/backend/FinanceTracker/Finance.Business/Mappers/TransactionMapper.cs` (namespace `Finance.Business.Mappers`) per [data-model.md §Mappers](data-model.md#mappers). `ToResponse` MUST throw `KeyNotFoundException` (with the offending transaction id and category id in the message) when `CategoryIds` references an unknown category. Implements FR-023, FR-024.

### Tests for User Story 4

- [X] T019 [P] [US4] Create `TransactionResponseRoundTripTests.cs` at `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Dtos/TransactionResponseRoundTripTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) serialize a `TransactionResponse` with two categories using `JsonSerializationOptions.Default` → JSON matches README's example (camelCase names, `categories` collection order, two-decimal `amount`, second-precision `timestamp`, `"Expense"` enum name), (b) deserialize back → field-by-field equality with the original. Use `Xunit.Assert` only.
- [X] T020 [P] [US4] Create `CategoryMapperTests.cs` at `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Mappers/CategoryMapperTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) `ToResponse` round-trips the three fields, (b) `ToEntity` carries the assigned id through. Use `Xunit.Assert` only.
- [X] T021 [P] [US4] Create `TransactionMapperTests.cs` at `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Mappers/TransactionMapperTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) `ToResponse` maps the six scalar fields + builds `Categories` in `CategoryIds` order, (b) `ToResponse` with a `categoriesById` missing one referenced id throws `KeyNotFoundException` with the offending transaction id + category id in the message (use `Assert.Throws<KeyNotFoundException>` + `Assert.Contains` on `ex.Message`), (c) `ToEntity` from the README's example `TransactionCreateRequest` payload carries the assigned id through and preserves `CategoryIds` (covers US3's Independent Test by reference). Use `Xunit.Assert` only.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~Finance.Business.UnitTests"` runs green for the three new test files. User Stories 1–4 are now independently complete.

---

## Phase 7: User Story 5 — Return a report as a summary with totals and a per-category breakdown (Priority: P2)

**Goal**: Define `ReportResult` (the aggregated summary the report endpoint will emit) and its `CategoryBreakdownItem` row.

**Independent Test**: Construct a `ReportResult` matching the README's ISO-week 2026-W20 example (one income transaction + four expense transactions, $1200 / $430.50 / $769.50, five-item `categoryBreakdown` in the documented sort order); serialize with `JsonSerializationOptions.Default`; confirm the JSON matches the README example field-for-field including the sort order of `categoryBreakdown`, the signed `total` values, and the `"IsoWeek"` enum name; separately construct an empty-period `ReportResult` and confirm `"categoryBreakdown": []` (not `null`, not absent).

### Implementation for User Story 5

- [X] T022 [P] [US5] Create `public sealed record CategoryBreakdownItem(string Category, decimal Total)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/CategoryBreakdownItem.cs` (namespace `Finance.Business.Dtos.Reports`) per [data-model.md §DTOs — Reports](data-model.md#dtos--reports). Implements FR-019. `Total` is signed; the sign carries direction (no separate direction field, no transaction count).
- [X] T023 [P] [US5] Create `public sealed record ReportResult(ReportType Type, string Period, decimal IncomeTotal, decimal ExpenseTotal, decimal NetTotal, IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/ReportResult.cs` (namespace `Finance.Business.Dtos.Reports`) per [data-model.md §DTOs — Reports](data-model.md#dtos--reports). Implements FR-018, FR-022. The DTO MUST NOT carry a `currency` field or a list of contributing transactions.

### Tests for User Story 5

- [X] T024 [US5] Create `ReportResultRoundTripTests.cs` at `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Dtos/ReportResultRoundTripTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) the README's ISO-week 2026-W20 example serializes field-for-field (including the five-item `categoryBreakdown` in income-first / expense-second, alphabetical-within-group order per FR-021, with signed `total` values), (b) an empty-period `ReportResult` (zero totals, empty `CategoryBreakdown`) serializes with `"categoryBreakdown": []` rather than `null` or absent (FR-022). Use `Xunit.Assert` only.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~ReportResultRoundTripTests"` runs green. User Stories 1–5 are now independently complete.

---

## Phase 8: User Story 6 — Submit a report request with a typed report type and per-type data payload (Priority: P3)

**Goal**: Define the report envelope (`ReportRequest`) and the MVP's only per-type payload `PeriodReportData`. `IsoWeekReportData` is **deferred** to a later feature (the IsoWeek strategy slice) per the [MVP scope note in spec.md US6](spec.md#user-story-6---submit-a-report-request-with-a-typed-report-type-and-a-per-type-data-payload-priority-p3). `ReportType.IsoWeek` remains in the enum as a documented future value, but no DTO is created for its `data` payload here.

**Independent Test**: Bind the README's example `Period` request (`{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }`) into `ReportRequest`; confirm `Type` is the enum value `Period` and `Data.Deserialize<PeriodReportData>(JsonSerializationOptions.Default)` exposes `Start`/`End` as `DateOnly` values. Confirm a request with `"type": "Quarter"` (or any value not in the enum — note `"IsoWeek"` IS valid since it's still in the enum, so use a value outside it) throws `JsonException` at the binding layer.

### Implementation for User Story 6

- [X] T025 [P] [US6] Create `public sealed record PeriodReportData(DateOnly Start, DateOnly End)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/PeriodReportData.cs` (namespace `Finance.Business.Dtos.Reports`) per [data-model.md §DTOs — Reports](data-model.md#dtos--reports). Implements FR-012. Inclusive bounds; contract does **not** enforce `Start ≤ End` (deferred to strategy).
- [X] T026 [P] [US6] Create `public sealed record ReportRequest(ReportType Type, JsonElement Data)` at `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/ReportRequest.cs` (namespace `Finance.Business.Dtos.Reports`; requires `using System.Text.Json;` + `using Finance.Business.Enums;`) per [data-model.md §DTOs — Reports](data-model.md#dtos--reports) and [research.md §1](research.md#1-how-to-represent-the-polymorphic-reportrequestdata-payload-in-c). Implements FR-011, FR-014.

> **Skipped (deferred)**: `IsoWeekReportData.cs` is NOT created in this feature. It ships with the IsoWeek strategy together with its strategy and tests. `ReportType.IsoWeek` stays in the foundational enum (T006) as a documented future value.

### Tests for User Story 6

- [X] T027 [US6] Create `ReportRequestRoundTripTests.cs` at `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Dtos/ReportRequestRoundTripTests.cs` per [quickstart.md §6](quickstart.md#6-add-the-tests). Cover: (a) serialize a `ReportRequest(ReportType.Period, …)` built from a `PeriodReportData` → JSON has `"type": "Period"` and `data: { "start": "…", "end": "…" }`; deserialize back; `JsonSerializer.Deserialize<PeriodReportData>(req.Data, JsonSerializationOptions.Default)` exposes the original `Start`/`End`, (b) deserializing `{ "type": "Quarter", "data": {} }` throws `JsonException` — use a value outside the enum's declared set (`"IsoWeek"` is **valid** here because it's still in the enum, so it would not trigger this rejection). Use `Xunit.Assert` only.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~ReportRequestRoundTripTests"` runs green. All six user stories are independently complete.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final validation + the constitution Principle V deliverable (AI-development log).

- [X] T028 Run `dotnet build` from `src/backend/FinanceTracker/` — MUST succeed with **zero warnings** (nullable warnings included; the records are non-nullable on every field).
- [X] T029 Run `dotnet test` from `src/backend/FinanceTracker/` — all tests in `Finance.Data.UnitTests` + `Finance.Business.UnitTests` MUST pass green. (`Finance.Api.UnitTests` and `Finance.Api.IntegrationTests` remain empty.)
- [X] T030 Append a single timestamped entry to `ai-artifacts/agent_log.txt` (per constitution v2.0.1 Principle V) summarizing this implementation run: model/tool, the prompt, the AI suggestion (this tasks.md execution), the decision (accepted), and reason.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 — the test projects can't compile foundational tests without the project references in Phase 1.
- **Phase 3 (US1)**: depends on T002 (test ref) + T004 (TransactionType enum).
- **Phase 4 (US2)**: depends on T002 (test ref) + T003 (test ref) + T005 (CategoryType enum) + T001 (Business → Data ref).
- **Phase 5 (US3)**: depends on T001 (Business → Data ref) + T004 (TransactionType enum).
- **Phase 6 (US4)**: depends on US1 (Transaction), US2 (Category + CategoryCreateRequest), US3 (TransactionCreateRequest), and Phase 2 foundational types.
- **Phase 7 (US5)**: depends on T006 (ReportType enum) + T007 (JsonSerializationOptions) + T003 (Business test ref).
- **Phase 8 (US6)**: depends on T006 (ReportType enum) + T007 (JsonSerializationOptions) + T003 (Business test ref).
- **Phase 9 (Polish)**: depends on all chosen story phases being complete.

### User Story Dependencies (high-level)

- **US1 (P1) → US4 (P2)**: US4's `TransactionMapper.ToResponse` consumes `Transaction`.
- **US2 (P1) → US4 (P2)**: US4's `CategoryMapper` consumes `Category` and `CategoryCreateRequest`. `TransactionResponse.Categories : IReadOnlyList<CategorySummary>` is built from `Category` data.
- **US3 (P1) → US4 (P2)**: US4's `TransactionMapper.ToEntity` consumes `TransactionCreateRequest`. US3 itself ships without dedicated tests; the mapper test (T021) covers the README round-trip in spec §US3 by reference.
- **US5 (P2) ⟂ US6 (P3)**: independent — different DTOs, different test files.
- **US5 and US6 ⟂ US1–US4**: independent at the contract level (no cross-references); both depend only on `ReportType` + `JsonSerializationOptions` from Phase 2.

### Within Each User Story

- Models / DTOs precede their tests (C# tests must compile against the SUT).
- Mappers presuppose both the entity and the DTO they translate between.
- Round-trip tests presuppose `JsonSerializationOptions.Default` (Phase 2).

### Parallel Opportunities

- **Phase 1**: T002 and T003 in parallel after T001 (T001 first because the next phase's references read from it, though there's no hard build-time dependency).
- **Phase 2**: T004, T005, T006, T007 all in parallel — four independent files in different projects/folders.
- **US1 internals**: T008 sequential with T009 (test needs SUT). No further parallelism within US1.
- **US2 internals**: T010 and T011 in parallel (different files, no inter-dependency); T012 after T010.
- **US4 internals**: T014, T015, T016 in parallel (three DTO files); T017 and T018 in parallel after the DTO files exist; T019, T020, T021 in parallel after the mappers exist.
- **US5 internals**: T022 and T023 in parallel (T023 declares `IReadOnlyList<CategoryBreakdownItem>` but compiles fine in any file order); T024 after both.
- **US6 internals**: T025 and T026 in parallel; T027 after both.
- **Cross-story**: once Phase 2 is done, US1+US2+US3 can run in parallel by different contributors; US4 waits until US1/US2/US3 each provide their entities/DTOs; US5 and US6 can run in parallel with anything once Phase 2 is done.

---

## Parallel Example: User Story 4

```text
# Once US1, US2, US3, and Phase 2 are done, all five DTO/mapper files can be created in parallel:
Task: "Create CategorySummary at Finance.Business/Dtos/Transactions/CategorySummary.cs"
Task: "Create TransactionResponse at Finance.Business/Dtos/Transactions/TransactionResponse.cs"
Task: "Create CategoryResponse at Finance.Business/Dtos/Categories/CategoryResponse.cs"
Task: "Create CategoryMapper at Finance.Business/Mappers/CategoryMapper.cs"
Task: "Create TransactionMapper at Finance.Business/Mappers/TransactionMapper.cs"

# Then all three test files in parallel:
Task: "Create TransactionResponseRoundTripTests at tests/Finance.Business.UnitTests/Dtos/TransactionResponseRoundTripTests.cs"
Task: "Create CategoryMapperTests at tests/Finance.Business.UnitTests/Mappers/CategoryMapperTests.cs"
Task: "Create TransactionMapperTests at tests/Finance.Business.UnitTests/Mappers/TransactionMapperTests.cs"
```

---

## Implementation Strategy

### MVP scope

This feature is **contracts-only** by design — there's no UI, no endpoint, no aggregation in scope. The narrowest *demonstrable* MVP is **US1 + US2 + US3** (the three P1 stories), which together let the Data layer describe transactions and categories and let the Business layer accept a transaction create payload. That gives the next feature (in-memory repositories) something concrete to seed and persist. US4–US6 deliver the response and report-request contracts that the period-report-strategy feature consumes.

If "single-story MVP" is strictly required, it is **US1 alone** (the `Transaction` record), since `Transaction` is the atomic unit of the entire product. US2 and US3 add the second pillar and the on-ramp respectively but cannot demonstrate value without US1.

### Incremental delivery

1. **Phase 1 + Phase 2** → project plumbing + shared infrastructure (`dotnet build` still clean).
2. **US1** → `Transaction` record + tests → first user-story checkpoint.
3. **US2 + US3** in parallel → all three P1 stories done; the Data layer is contract-complete for entities.
4. **US4** → response DTOs + mappers → the API can return data once endpoints are added (future feature).
5. **US5 + US6** in parallel → report contracts ready for the period-report-strategy feature.
6. **Phase 9 polish** → final build + test green, `agent_log.txt` entry appended.

### Parallel team strategy

With multiple contributors (single dev here, but for completeness):

1. Complete Phase 1 + Phase 2 collaboratively.
2. After Phase 2:
   - Dev A: US1 (Transaction + tests)
   - Dev B: US2 (Category + CategoryCreateRequest + tests)
   - Dev C: US3 (TransactionCreateRequest)
3. After all three P1 stories: Dev A picks up US4; Devs B and C pick up US5 and US6 in parallel.
4. Final Phase 9 polish is single-developer (the build/test verification is monolithic).

---

## Notes

- **[P] markers** are file-disjoint within a phase. Cross-phase tasks may still parallelize if their dependencies in earlier phases are complete, but the phase header is the authoritative gating signal.
- **[Story] labels** map every implementation task back to a spec `User Story N`, satisfying the constitution v2.0.1 Principle IV obligation that the test suite cover each user-story-level invariant.
- **No FluentAssertions anywhere** — all assertions use the built-in `Xunit.Assert` API per constitution v2.0.1 Principle IV. Don't add `FluentAssertions` to either test csproj.
- **No new NuGet packages**. Both test csproj's already include `xunit.v3` 3.2.2 (`OutputType=Exe`). The only csproj edits are `<ProjectReference>` entries (Phase 1).
- **No source files in Finance.Api** and **no files in `Finance.Api.UnitTests` / `Finance.Api.IntegrationTests`** in this feature — those test projects stay as empty scaffolds until controller/endpoint features land.
- **Constitution v2.0.1 Principle V** (T031): the `ai-artifacts/agent_log.txt` entry is a graded deliverable; don't skip it.
- **Out of scope** (see [spec.md §FR-028–FR-030](spec.md#requirements-mandatory)): no repositories, no controllers, no validation enforcement, no report-strategy execution, no export. Resist adding any of these here.
