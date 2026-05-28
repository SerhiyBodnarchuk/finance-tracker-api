# Tasks: Transaction Export (JSON / CSV)

**Input**: Design documents from `specs/006-transaction-export/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/http-endpoint.md ✅

**Tests**: Included — the constitution (Principle IV) explicitly requires export logic to be covered in `Finance.Business.UnitTests` (format generation) and `Finance.Api.IntegrationTests` (content negotiation).

**Organization**: Tasks are grouped by user story. US1 (JSON) and US3 (406) share the controller action; US2 (CSV) adds the CSV path on top of US1. The foundational phase ships the CSV formatter so it can be unit-tested before any HTTP concerns are touched.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unresolved dependencies)
- **[Story]**: Maps to user story (US1 = JSON export, US2 = CSV export, US3 = 406 rejection)

---

## Phase 1: Setup

**Purpose**: Confirm the build is green before making changes.

- [x] T001 Run `dotnet build` and `dotnet test` from `src/backend/FinanceTracker` and confirm all projects compile and all existing tests pass (no new files — baseline check only)

---

## Phase 2: Foundational — CSV Formatter (Business Layer)

**Purpose**: Implement and fully test `TransactionCsvFormatter` in `Finance.Business` before any HTTP concern is introduced. This is independently testable as a pure unit — no controller, no HTTP client needed.

**⚠️ CRITICAL**: US2 (CSV export) cannot be implemented correctly without this formatter being complete and passing its unit tests.

- [x] T002 Create `Finance.Business/Export/TransactionCsvFormatter.cs` — static class with `public static string Format(IEnumerable<TransactionResponse> transactions)`. Columns: `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds`. Apply RFC 4180 quoting (wrap in `"..."`, double internal `"`) when a field contains `,`, `"`, `\r`, or `\n`. Format `Amount` with `CultureInfo.InvariantCulture`, `Timestamp` as `yyyy-MM-ddTHH:mm:ss`, `CategoryIds` as semicolon-delimited integer list.
- [x] T003 [P] Create `tests/Finance.Business.UnitTests/Export/TransactionCsvFormatterTests.cs` — write the following xUnit v3 tests using `Xunit.Assert` (no FluentAssertions, no Moq needed — formatter is static):
  - `EmptyList_ReturnsHeaderRowOnly` — output has exactly one line (`Id,Description,Amount,Type,Timestamp,CategoryIds`)
  - `SingleTransaction_SingleCategory_FormatsAllColumns` — verify all six columns present with correct values
  - `SingleTransaction_MultipleCategories_SemicolonDelimited` — `CategoryIds` cell is `"1;2"` (or appropriate IDs)
  - `Description_WithComma_IsRfc4180Quoted` — description wrapped in `"..."`
  - `Description_WithDoubleQuote_DoublesTheQuote` — internal `"` becomes `""`
  - `Amount_UsesInvariantCulture` — no locale-specific decimal separator
  - `Timestamp_FormattedAsIso8601NoZone` — matches `yyyy-MM-ddTHH:mm:ss` pattern

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TransactionCsvFormatterTests"` must pass before continuing.

---

## Phase 3: User Story 1 — JSON Export (Priority: P1) 🎯 MVP

**Goal**: `GET /api/transactions/export` with `Accept: application/json` (or absent / `*/*`) returns 200, `Content-Type: application/json`, `Content-Disposition: attachment; filename=transactions.json`, and a JSON array of all transactions.

**Independent Test**: Start the API and run `Invoke-WebRequest -Uri "http://localhost:5182/api/transactions/export" -Headers @{ Accept = "application/json" }` — verify 200 status and a JSON array body containing the seeded transactions.

### Tests for User Story 1

- [x] T004 [P] [US1] Add the following integration tests to `tests/Finance.Api.IntegrationTests/Controllers/TransactionsExportTests.cs` (create the file; use `WebApplicationFactory<Program>` and `HttpClient`):
  - `AcceptJson_Returns200` — `Accept: application/json` → 200
  - `AcceptJson_ContentTypeIsJson` — response `Content-Type` starts with `application/json`
  - `AcceptJson_ContentDispositionIsAttachment` — response header `Content-Disposition` equals `attachment; filename=transactions.json`
  - `AcceptJson_BodyIsJsonArray` — body deserialises to a non-null JSON array
  - `AcceptWildcard_DefaultsToJson` — `Accept: */*` → 200 with `Content-Type` starting with `application/json`
  - `NoAcceptHeader_DefaultsToJson` — no `Accept` header → 200 with `Content-Type` starting with `application/json`

### Implementation for User Story 1

- [x] T005 [US1] Add `Export` action to `Finance.Api/Controllers/TransactionsController.cs`:
  ```
  [HttpGet("export")]
  public IActionResult Export()
  ```
  — use `Request.GetTypedHeaders().Accept` (quality-sorted `IList<MediaTypeHeaderValue>`); iterate in order; if empty list or first match is `application/json` / `*/*` → append `Content-Disposition: attachment; filename=transactions.json` header and return `Ok(transactions.GetAll())`. For now, any unrecognised type → serve JSON (US3 will add 406 in Phase 5). Add `using Microsoft.Net.Http.Headers;` if not already present.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.AcceptJson"` must pass; `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.AcceptWildcard"` must pass; `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.NoAcceptHeader"` must pass.

---

## Phase 4: User Story 2 — CSV Export (Priority: P2)

**Goal**: `GET /api/transactions/export` with `Accept: text/csv` returns 200, `Content-Type: text/csv; charset=utf-8`, `Content-Disposition: attachment; filename=transactions.csv`, and an RFC 4180 CSV body with a header row followed by one row per transaction.

**Independent Test**: Start the API; run `Invoke-WebRequest -Uri "http://localhost:5182/api/transactions/export" -Headers @{ Accept = "text/csv" } -OutFile "transactions.csv"` — open `transactions.csv` in a spreadsheet; verify columns `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds` are present and all seeded transactions appear.

**Depends on**: T002–T003 (formatter) and T005 (Export action skeleton).

### Tests for User Story 2

- [x] T006 [P] [US2] Add the following tests to `tests/Finance.Api.IntegrationTests/Controllers/TransactionsExportTests.cs` (same file as T004):
  - `AcceptCsv_Returns200`
  - `AcceptCsv_ContentTypeIsCsv` — response `Content-Type` starts with `text/csv`
  - `AcceptCsv_ContentDispositionIsAttachment` — header `Content-Disposition` equals `attachment; filename=transactions.csv`
  - `AcceptCsv_BodyStartsWithHeaderRow` — first line of body is `Id,Description,Amount,Type,Timestamp,CategoryIds`
  - `AcceptCsv_BodyContainsAllSeededTransactions` — line count equals seeded transaction count + 1 (header)
  - `QualityWeighted_JsonHigherQuality_ReturnsJson` — `Accept: text/csv;q=0.5, application/json;q=1.0` → `Content-Type` starts with `application/json`

### Implementation for User Story 2

- [x] T007 [US2] Extend `Finance.Api/Controllers/TransactionsController.cs` — inside the `Export` action, add the `text/csv` branch: when `Accept` resolves to `text/csv`, call `TransactionCsvFormatter.Format(transactions.GetAll())`, append `Content-Disposition: attachment; filename=transactions.csv`, and return `Content(csv, "text/csv; charset=utf-8")`. Add `using Finance.Business.Export;` to the controller.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.AcceptCsv"` and `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.QualityWeighted"` must pass.

---

## Phase 5: User Story 3 — Unsupported Format Rejection (Priority: P3)

**Goal**: `GET /api/transactions/export` with `Accept: application/xml` (or any other unsupported-only media type) returns 406 Not Acceptable with an empty body.

**Independent Test**: `Invoke-RestMethod -Uri "http://localhost:5182/api/transactions/export" -Headers @{ Accept = "application/xml" }` → `406 Not Acceptable`.

**Depends on**: T005 and T007 (Export action with JSON + CSV branches).

### Tests for User Story 3

- [x] T008 [P] [US3] Add the following tests to `tests/Finance.Api.IntegrationTests/Controllers/TransactionsExportTests.cs`:
  - `AcceptXml_Returns406` — `Accept: application/xml` → HTTP status 406

### Implementation for User Story 3

- [x] T009 [US3] Extend `Finance.Api/Controllers/TransactionsController.cs` — in the `Export` action, replace the fallback "serve JSON for anything" behaviour with `StatusCode(StatusCodes.Status406NotAcceptable)` when no `Accept` value matches `application/json`, `*/*`, or `text/csv`. Ensure the empty-list (no `Accept` header) case still defaults to JSON (handled before the loop, not at the fallback).

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TransactionsExportTests.AcceptXml"` must pass. Run `dotnet test` in full to confirm all tests still pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T010 [P] Run `dotnet test` from `src/backend/FinanceTracker` — confirm **all** tests pass (zero failures, zero skips)
- [x] T011 Append an entry to `ai-artifacts/agent_log.txt` documenting this feature: timestamp, model/tool used, what was accepted/rejected, and the rationale

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — baseline check only
- **Phase 2 (Foundational)**: Depends on Phase 1 — `TransactionCsvFormatter` must be complete before US2 controller work (T007)
- **Phase 3 (US1 - JSON)**: Depends on Phase 1 only — no dependency on formatter
- **Phase 4 (US2 - CSV)**: Depends on Phase 2 (T002–T003) and Phase 3 (T005)
- **Phase 5 (US3 - 406)**: Depends on Phase 3 (T005) and Phase 4 (T007)
- **Phase 6 (Polish)**: Depends on all prior phases

### User Story Dependencies

- **US1 (P1)**: Independent after Phase 1
- **US2 (P2)**: Depends on Foundational (formatter) and US1 (Export action skeleton)
- **US3 (P3)**: Depends on US1 and US2 (Export action with both branches complete)

### Within Each Phase

- Tests (T003, T004, T006, T008) MUST be written before the corresponding implementation tasks are marked complete
- For unit tests (T003): write → run → confirm red → implement (T002) → confirm green
- For integration tests (T004, T006, T008): write → run → confirm red → implement (T005, T007, T009) → confirm green

### Parallel Opportunities

- T002 and T003 can be written in parallel (different files) and combined at run time
- T004 and T005 can be drafted in parallel but T005 must pass before T004 is marked green
- T006 and T007 can be drafted in parallel; same constraint

---

## Parallel Example: Phase 2 (Foundational)

```
# Draft in parallel:
Task T002: Create TransactionCsvFormatter.cs in Finance.Business/Export/
Task T003: Create TransactionCsvFormatterTests.cs in Finance.Business.UnitTests/Export/

# Then run together:
dotnet test --filter "FullyQualifiedName~TransactionCsvFormatterTests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational — formatter + unit tests (T002, T003)
3. Complete Phase 3: US1 — JSON export action + integration tests (T004, T005)
4. **STOP and VALIDATE**: `dotnet test` all green; `GET /api/transactions/export` with `Accept: application/json` returns a downloadable JSON file
5. Ship or demo

### Incremental Delivery

1. MVP (US1) as above
2. Add US2 (CSV): T006, T007 → validate `text/csv` download
3. Add US3 (406): T008, T009 → validate rejection behaviour
4. Polish: T010, T011

---

## Notes

- `[P]` tasks are different files with no shared write-dependencies — they can be started concurrently
- Each user story phase ends with a testable checkpoint; do not advance until the checkpoint passes
- `TransactionCsvFormatter` is a static class — no DI registration, no interface, no changes to `Program.cs`
- The `Export` action is added to the existing `TransactionsController` — no new controller, no new route prefix
- `dotnet test` in CI already covers `Finance.Business.UnitTests` and `Finance.Api.IntegrationTests` — no CI changes needed
