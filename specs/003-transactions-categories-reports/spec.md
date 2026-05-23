# Feature Specification: Transactions / Categories Endpoints with Business Validation and Period Reports

**Feature Branch**: `003-transactions-categories-reports`

**Created**: 2026-05-21

**Status**: Draft

**Input**: User description: "Add all needed api endpoints with simple validation, and business logic as well (including period report generation). Export to json/csv may be skipped. Some examples can be used from ai-artifacts/Specifications/period-report-strategy-spec.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read transactions and categories over HTTP (Priority: P1)

A developer or evaluator opens the API host's Scalar UI (or fires `curl`) and immediately sees the seeded transactions and categories returned over HTTP, with a way to fetch a single record by its identifier. No body parsing for these reads — just a GET and a JSON response shape that exposes the data without exposing internal domain types.

**Why this priority**: This is the smallest end-to-end slice that proves the three-layer pipeline works: HTTP request → controller → business-layer mapper → repository → DTO response. Until reads work, neither writes nor reports can be demonstrated end-to-end. It's also the first time the API surface is reachable from outside the process; every later story relies on it.

**Independent Test**: Start the API host. `GET /api/transactions` returns HTTP 200 with a JSON array containing the five seeded transactions in id order; `GET /api/categories` does the same for categories. `GET /api/transactions/3` and `GET /api/categories/2` each return HTTP 200 with the matching record. `GET /api/transactions/99999` and `GET /api/categories/99999` each return HTTP 404 with a problem-details body.

**Acceptance Scenarios**:

1. **Given** the API is running on a fresh process, **When** a caller sends `GET /api/transactions`, **Then** the response is HTTP 200 with a JSON array of five transaction response objects matching the seeded data, ordered by id ascending.
2. **Given** the API is running on a fresh process, **When** a caller sends `GET /api/categories`, **Then** the response is HTTP 200 with a JSON array of five category response objects matching the seeded data, ordered by id ascending.
3. **Given** a seeded transaction with id `3` exists, **When** a caller sends `GET /api/transactions/3`, **Then** the response is HTTP 200 with that single transaction's response body.
4. **Given** no transaction with id `99999` exists, **When** a caller sends `GET /api/transactions/99999`, **Then** the response is HTTP 404 with a problem-details body identifying the not-found resource.
5. **Given** a transaction response is returned, **When** a consumer inspects the body, **Then** the body exposes the response DTO shape — never a raw domain entity. (The API layer never serializes a domain type.)

---

### User Story 2 - Create and delete transactions and categories, with validation (Priority: P2)

A consumer adds a new transaction or category via `POST`, gets the created record back with its server-assigned id, and can later remove a transaction by id. Invalid input is rejected at the HTTP boundary with a `400 Bad Request` and a clear error description; duplicate category names are rejected with `409 Conflict`. The validation rules deferred from feature 002 (`Amount > 0`, category referential integrity, transaction-type ↔ category-type compatibility) live in the business layer and are exercised here.

**Why this priority**: Demonstrating writes is the second-most-important capability for a financial-tracking API. Reports and exports both depend on the consumer being able to add data they care about (one income source, several expenses) rather than only working off the seed set. Writes also exercise the validation surface, which is what differentiates a real API from the bare repository layer.

**Independent Test**: With the API running, `POST /api/transactions` with a valid body returns HTTP 201 plus a `Location` header pointing at the new resource and a response body with `id` set; immediately fetching that `Location` returns 200 with the same record. `POST /api/categories` with a valid body behaves symmetrically. A second `POST /api/categories` with a name that already exists (different case) returns HTTP 409. A `POST /api/transactions` with `amount: 0` returns HTTP 400; with a non-existent `categoryIds` entry returns HTTP 400; with an `Income` transaction tagged with an `Expense`-only category returns HTTP 400. `DELETE /api/transactions/{id}` returns HTTP 204 the first time, HTTP 404 the second.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller sends `POST /api/transactions` with a valid body (amount > 0, all `categoryIds` exist and are compatible with the chosen `type`), **Then** the response is HTTP 201 with a `Location: /api/transactions/{newId}` header and the body is the response DTO for the stored record with its server-assigned id.
2. **Given** the API is running, **When** a caller sends `POST /api/categories` with a valid body, **Then** the response is HTTP 201 with a `Location: /api/categories/{newId}` header and the body is the response DTO for the stored category.
3. **Given** a category named `"Groceries"` exists, **When** a caller sends `POST /api/categories` with `name: "GROCERIES"`, **Then** the response is HTTP 409 Conflict with a problem-details body explaining the duplicate name.
4. **Given** the API is running, **When** a caller sends `POST /api/transactions` with `amount: 0` (or any value ≤ 0), **Then** the response is HTTP 400 with a problem-details body identifying `amount` as the failing field.
5. **Given** the API is running, **When** a caller sends `POST /api/transactions` with an empty `categoryIds` array, **Then** the response is HTTP 400 with a problem-details body identifying `categoryIds` as the failing field.
6. **Given** no category with id `9999` exists, **When** a caller sends `POST /api/transactions` with `categoryIds: [9999]`, **Then** the response is HTTP 400 with a problem-details body identifying the unknown category id.
7. **Given** category `1` is `Salary` (Income-only), **When** a caller sends `POST /api/transactions` with `type: "Expense"` and `categoryIds: [1]`, **Then** the response is HTTP 400 with a problem-details body explaining the type-compatibility mismatch.
8. **Given** a transaction with id `2` exists, **When** a caller sends `DELETE /api/transactions/2`, **Then** the response is HTTP 204 No Content; a subsequent `GET /api/transactions/2` returns HTTP 404.
9. **Given** no transaction with id `99999` exists, **When** a caller sends `DELETE /api/transactions/99999`, **Then** the response is HTTP 404 without mutating the store.

---

### User Story 3 - Generate a period report (Priority: P3)

A consumer asks the API to summarize income, expenses, net, and per-category totals for a given date range and gets back a single JSON response with the four totals and a sorted category breakdown. The breakdown reflects multi-category attribution — a transaction tagged with multiple categories contributes its full amount to each of those breakdown lines — and the response intentionally does **not** echo the underlying transactions back.

**Why this priority**: Reports are the value the consumer is actually here for. A finance tracker is a transaction CRUD app until it can summarize a period — at which point it earns its name. This story exercises the report factory + strategy pattern that the project will use as its reusable AI scaffolding template for future report types.

**Independent Test**: With the seed data loaded, `POST /api/reports` with `{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }` returns HTTP 200 with `incomeTotal: 1200.00`, `expenseTotal: 121.29`, `netTotal: 1078.71`, a `categoryBreakdown` containing five items in the documented sort order, and a `period: "2026-05-01..2026-05-31"` descriptor. No `transactions` array, no `currency` field. A request with `start > end` returns HTTP 400. A request with `type: "Month"` or any value outside the `ReportType` enum returns HTTP 400. A request with `type: "IsoWeek"` returns HTTP 400 because the IsoWeek strategy is documented as not yet implemented (the enum value exists in the contract but the factory does not yet resolve it).

**Acceptance Scenarios**:

1. **Given** the seed data is loaded, **When** a caller sends `POST /api/reports` with body `{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }`, **Then** the response is HTTP 200 with body fields `type: "Period"`, `period: "2026-05-01..2026-05-31"`, `incomeTotal: 1200.00`, `expenseTotal: 121.29`, `netTotal: 1078.71`, and `categoryBreakdown` containing five items, one per seeded category, sorted income-side-first (positive totals) then expense-side (negative totals), alphabetical within each group.
2. **Given** the seed data is loaded, **When** a caller sends a period report request that includes a date range covering a transaction tagged with two categories `[A, B]`, **Then** that transaction's full signed amount appears in **both** category A's and category B's breakdown lines — i.e., the arithmetic sum of `categoryBreakdown[*].total` may exceed `netTotal` in absolute value when multi-tagged transactions are present.
3. **Given** the API is running, **When** a caller sends a `Period` report request with `start: "2026-05-31", end: "2026-05-01"` (`start > end`), **Then** the response is HTTP 400 with a problem-details body explaining the range is invalid.
4. **Given** the API is running, **When** a caller sends a report request with `type: "Month"`, **Then** the response is HTTP 400 because the `ReportType` enum does not include `Month`.
5. **Given** the API is running, **When** a caller sends a report request with `type: "IsoWeek"`, **Then** the response is HTTP 400 explaining that the IsoWeek strategy is not yet available. (`IsoWeek` is a documented future value of the `ReportType` enum but its strategy is intentionally out of scope here.)
6. **Given** the API is running, **When** a caller sends a `Period` report request with `data: {}` (missing `start` or `end`), **Then** the response is HTTP 400 with a problem-details body identifying the missing field.
7. **Given** the API is running, **When** two `Period` report requests with identical inputs are sent back-to-back with no intervening writes, **Then** the two response bodies are byte-identical (reports are ad-hoc, never cached or persisted).
8. **Given** a `Period` report response is returned, **When** a consumer inspects the body, **Then** the body contains exactly the six fields `type`, `period`, `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown` — **no** `transactions` array, **no** `currency` field.

---

### Edge Cases

- A transaction is created with multiple categories that mix `Both` and a `TransactionType`-compatible type (e.g., `Expense` transaction with categories `Groceries (Expense)` + `Misc (Both)`) — accepted as valid.
- A transaction is created with multiple categories where one is incompatible (e.g., `Expense` transaction with `Salary (Income)` + `Groceries (Expense)`) — rejected with HTTP 400 identifying the single offending category.
- A `Period` report request includes a category that has zero transactions in the window — that category does **not** appear in `categoryBreakdown` (only categories with at least one matching transaction show up).
- A `Period` report request covers a window with zero transactions — `incomeTotal: 0`, `expenseTotal: 0`, `netTotal: 0`, `categoryBreakdown: []`. HTTP 200, not 404.
- `POST /api/transactions` with `timestamp` in the future (e.g., year 3000) is accepted — there is no upper-bound check, and there is no realistic forecasting concern in the MVP.
- `POST /api/categories` with a name that contains only whitespace (e.g., `"   "`) is rejected as empty — the validator trims before checking emptiness, but does **not** trim before checking case-insensitive uniqueness (see Assumptions).
- A request body with extra unknown fields is accepted; unknown fields are silently ignored. (Standard JSON-deserialization behavior; strict deserialization is not in scope here.)
- Transaction descriptions may contain sensitive text. No redaction or pruning is performed in this feature — that is the responsibility of the future MCP context layer (per constitution Principle V).
- API documentation: the OpenAPI document at `/openapi/v1.json` and the Scalar reference UI at `/scalar/v1` remain dev-only and are not exposed in Release builds.

## Requirements *(mandatory)*

### Functional Requirements

**Transactions API**:

- **FR-001**: The system MUST expose `GET /api/transactions` returning HTTP 200 with a JSON array of all stored transactions in id-ascending order. The body MUST use the transaction response DTO shape, never a domain entity.
- **FR-002**: The system MUST expose `GET /api/transactions/{id}` returning HTTP 200 with the transaction response DTO when the id exists, and HTTP 404 with a problem-details body when it does not.
- **FR-003**: The system MUST expose `POST /api/transactions` accepting a transaction create-request body. On success, the response MUST be HTTP 201 with a `Location` header of `/api/transactions/{newId}` and the body MUST be the transaction response DTO for the stored record (including its server-assigned id and resolved category summaries).
- **FR-004**: The system MUST expose `DELETE /api/transactions/{id}` returning HTTP 204 No Content when the id exists, and HTTP 404 with a problem-details body when it does not.

**Categories API**:

- **FR-005**: The system MUST expose `GET /api/categories` returning HTTP 200 with a JSON array of all stored categories in id-ascending order. The body MUST use the category response DTO shape.
- **FR-006**: The system MUST expose `GET /api/categories/{id}` returning HTTP 200 with the category response DTO when the id exists, and HTTP 404 with a problem-details body when it does not.
- **FR-007**: The system MUST expose `POST /api/categories` accepting a category create-request body. On success, the response MUST be HTTP 201 with a `Location` header of `/api/categories/{newId}` and the body MUST be the category response DTO.
- **FR-008**: A `POST /api/categories` whose `name` collides with an existing category's name compared case-insensitively MUST be rejected with HTTP 409 Conflict and a problem-details body explaining the duplicate.

**Validation rules** (business layer, applied before any repository write):

- **FR-009**: A transaction create request with `amount` not strictly greater than zero MUST be rejected with HTTP 400 and a problem-details body identifying `amount` as the failing field.
- **FR-010**: A transaction create request with an empty or missing `description` (after trimming) MUST be rejected with HTTP 400 identifying `description` as the failing field.
- **FR-011**: A transaction create request with an empty or missing `categoryIds` array MUST be rejected with HTTP 400 identifying `categoryIds` as the failing field.
- **FR-012**: A transaction create request whose `categoryIds` list contains an id that does not resolve to an existing category MUST be rejected with HTTP 400, with a problem-details body that names the offending unknown id.
- **FR-013**: A transaction create request whose `type` is incompatible with any of its referenced categories' `type` MUST be rejected with HTTP 400, with a problem-details body naming the offending category. Compatibility rule (mirror of constitution Principle III): an `Expense` transaction MAY reference categories of type `Expense` or `Both`; an `Income` transaction MAY reference categories of type `Income` or `Both`.
- **FR-014**: A category create request with an empty or whitespace-only `name` MUST be rejected with HTTP 400 identifying `name` as the failing field.
- **FR-015**: All business validation MUST live in the Business layer (a dedicated validator service or equivalent), not in controllers. Controllers MUST translate validation failures into HTTP status codes and problem-details bodies but MUST NOT themselves enforce validation rules.

**Reports API**:

- **FR-016**: The system MUST expose `POST /api/reports` accepting an envelope of `{ type, data }` where `type` is a value of the `ReportType` enum and `data` is the payload that matches that type (`PeriodReportData` for `type: "Period"`).
- **FR-017**: A report request with `type` outside the `ReportType` enum (e.g., `"Month"`) MUST be rejected with HTTP 400.
- **FR-018**: A report request with `type: "IsoWeek"` MUST be rejected with HTTP 400 with a problem-details body explaining that the IsoWeek strategy is not yet available. (`IsoWeek` is a known future enum value; only `Period` is in scope for this feature.)
- **FR-019**: A `Period` report request whose `data` is missing `start` or `end`, has either field in non-ISO-`yyyy-MM-dd` form, or has `start > end` MUST be rejected with HTTP 400 identifying the failing field.
- **FR-020**: A valid `Period` report request MUST return HTTP 200 with a body containing exactly the six fields `type`, `period`, `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown`. The body MUST NOT contain a `transactions` array, a `currency` field, or any other fields beyond the six.
- **FR-021**: The `Period` report MUST include every transaction whose `timestamp` falls in the inclusive window `[start 00:00:00, end 23:59:59]` in local time, and MUST exclude every transaction outside that window.
- **FR-022**: `incomeTotal` and `expenseTotal` in the report response MUST each be the sum of positive (unsigned) amounts of the corresponding-type transactions in the window. Each is positive-or-zero. `netTotal = incomeTotal − expenseTotal` and may be negative.
- **FR-023**: The `period` field in the report response MUST be the string `"yyyy-MM-dd..yyyy-MM-dd"` formed from the request's `start` and `end`.
- **FR-024**: `categoryBreakdown` MUST contain one item per category that has at least one matching transaction in the window. Each item has fields `category` (the category's name) and `total` (a signed decimal). No transaction count, no direction field, no other fields.
- **FR-025**: A transaction tagged with N categories MUST contribute its full signed amount to **each** of those N breakdown items. The arithmetic sum of `categoryBreakdown[*].total` MAY therefore exceed `netTotal` in absolute value when multi-tagged transactions are present — this is by design.
- **FR-026**: `categoryBreakdown` MUST be sorted income-side first (items with positive `total`; zero-total items from `Both`-type categories sort here), then expense-side (negative `total`); within each of those two groups, by category `name` ascending.
- **FR-027**: Reports are ad-hoc — every response MUST be computed fresh from current entity state on every request. The system MUST NOT persist, cache, or otherwise store any report response. Two requests with identical input and unchanged entity state MUST produce byte-identical responses.

**Architecture / cross-cutting**:

- **FR-028**: The report endpoint MUST resolve the requested `ReportType` enum value through a factory to an `IReportStrategy`-shaped object. The controller MUST NOT branch on `type`, parse the `data` payload, or perform aggregation directly — each strategy owns its full parse → validate → aggregate → return pipeline.
- **FR-029**: Only the period strategy MUST be scaffolded in this feature. The `IsoWeek` strategy MUST NOT be created.
- **FR-030**: Repositories MUST continue to be registered as singletons. The business-layer validator and the report strategy / factory SHOULD be registered with whatever lifetime keeps them stateless and reusable (scoped or singleton; the implementer chooses, as long as they have no per-request mutable state).
- **FR-031**: The API layer MUST NOT receive or return domain entities. All HTTP payloads MUST use the DTOs defined in the Business layer.

### Key Entities

- **Transaction Create Request** (HTTP request body for `POST /api/transactions`): `timestamp` (ISO 8601 date-time), `description` (string, required, non-empty), `amount` (decimal > 0), `type` (`"Income"` | `"Expense"`), `categoryIds` (non-empty array of existing category ids).
- **Transaction Response** (HTTP response body): server-assigned `id`, `timestamp`, `description`, `amount`, `type`, plus a `categories` array of `{id, name}` summary records (one per attached category, in input order).
- **Category Create Request**: `name` (string, required, non-empty after trim), `type` (`"Income"` | `"Expense"` | `"Both"`).
- **Category Response**: server-assigned `id`, `name`, `type`.
- **Report Request envelope**: `type` (a `ReportType` enum value: `"Period"` or `"IsoWeek"` — but only `Period` is implemented), `data` (a payload object whose shape depends on `type`).
- **Period Report Data**: `start` (ISO date `yyyy-MM-dd`), `end` (ISO date `yyyy-MM-dd`), both inclusive bounds, `start <= end`.
- **Report Result** (HTTP response body for any successful `POST /api/reports`): `type`, `period` (string descriptor), `incomeTotal` (decimal ≥ 0), `expenseTotal` (decimal ≥ 0), `netTotal` (decimal, may be negative), `categoryBreakdown` (sorted array of `{category, total}`). No `transactions` array, no `currency` field — by constitution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer running the API on a fresh checkout can list all five seeded transactions and all five seeded categories over HTTP, fetch one by id, create a new transaction with valid input, get back HTTP 201 with a `Location` header, fetch the new record at that location, delete it, and confirm a follow-up GET returns HTTP 404 — all in a single Scalar UI or `curl` session without any other setup.
- **SC-002**: 100% of the validation acceptance scenarios for User Story 2 produce the documented HTTP status code (400 for amount/description/categoryIds/unknown-category-id/type-mismatch; 409 for duplicate name) and a problem-details body that names the failing field or constraint.
- **SC-003**: A `Period` report over the full seed window (`2026-05-01..2026-05-31`) returns `incomeTotal = 1200.00`, `expenseTotal = 121.29`, `netTotal = 1078.71`, and a five-item `categoryBreakdown` sorted per the documented rule.
- **SC-004**: For any valid `Period` report request, two consecutive calls with no intervening writes produce byte-identical response bodies (reports are ad-hoc, never cached).
- **SC-005**: A `Period` report request whose window covers a transaction tagged with multiple categories shows that transaction's amount in **every** matching category's breakdown line — and the arithmetic sum of `categoryBreakdown[*].total` differs from `netTotal` by exactly the duplicated contribution. (Multi-category attribution invariant.)
- **SC-006**: All required test coverage areas mandated by the constitution's Principle IV that map to this feature (validation, factory selection by enum value, period aggregation including the multi-category attribution invariant and the boundary edge cases, category breakdown sort order, API endpoint integration coverage) have at least one passing test each. The full test suite finishes in under 15 seconds on a developer machine.
- **SC-007**: The OpenAPI document served at `/openapi/v1.json` (in Development only) lists every endpoint in this feature with its request and response shapes documented, including the report envelope's per-type `data` payload.

## Assumptions

- The `Period` report bounds are **inclusive** on the date axis. The strategy applies the rule `[start 00:00:00, end 23:59:59]` against each transaction's `timestamp`. This is the canonical behavior from constitution Principle II and is non-negotiable in this feature.
- `categoryBreakdown` includes only categories with at least one matching transaction in the window — zero-activity categories are omitted from the response. This is the standard "non-empty groups only" convention used by aggregation reports.
- `incomeTotal` and `expenseTotal` are positive-or-zero unsigned sums; the **signed** amounts (positive for income, negative for expense) only appear inside `categoryBreakdown[*].total`. This split is what constitution Principle II requires.
- `Both`-type categories may legitimately have a zero `total` when their attached transactions cancel out (or when only one direction's transactions land in the window). They sort with the income-side (positive-or-zero) group, alphabetically by name, per the constitution.
- Category name comparisons for case-insensitive uniqueness use `StringComparison.OrdinalIgnoreCase` (the rule established by feature 002). Trimming is applied to the non-empty check but not to the uniqueness check — `" Groceries"` is technically a distinct name from `"Groceries"` for storage purposes (the validator considers it "not empty after trim" but lets it through as a different name). A future feature may decide to trim everywhere; out of scope here.
- HTTP status codes follow REST conventions: 201 with `Location` for successful create, 204 for successful delete, 400 for input validation problems, 404 for not-found on GET/DELETE, 409 for duplicate-name on category create, 200 with body for successful reads and report computations. Problem-details bodies follow RFC 7807.
- The OpenAPI / Scalar endpoints already configured in `Program.cs` continue to be gated behind `app.Environment.IsDevelopment()`. This feature does not change that gating.
- JSON/CSV export is **explicitly out of scope** per the user's request. The export endpoints will land in a separate feature.
- Transaction descriptions may contain sensitive text. This feature does not redact or prune them — the MCP context layer (future feature) is the layer responsible for that, per constitution Principle V. PII is not introduced into any test fixture in this feature.
- The MCP context / replay milestone is out of scope.
- `IsoWeek` is intentionally **declared but not implemented**. The `ReportType` enum already has the value (it landed in feature 001); a request that passes `type: "IsoWeek"` reaches the factory, fails to resolve, and returns HTTP 400. The `IsoWeekReportData` payload DTO is NOT scaffolded in this feature — that ships with the eventual IsoWeek strategy feature.
