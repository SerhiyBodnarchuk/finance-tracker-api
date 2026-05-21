# Feature Specification: Domain Entities and API DTOs

**Feature Branch**: `001-domain-entities-dtos`

**Created**: 2026-05-19

**Status**: Draft (revised 2026-05-21 — `IsoWeek` scoped out to a future feature; see the note above the Functional Requirements section)

**Input**: User description: "Need to create domain entities in data project along with according DTOs (use records). Retrieve data from README.md. Also there is task description: lightweight tracker for income and expenses with categories and a weekly summary; focus on correct aggregation and category handling. MVP API to add transactions, tag categories, and return weekly totals and category breakdowns. Export results to CSV or JSON. Tests for aggregation logic and edge cases for overlapping week boundaries; integration test for end-to-end transaction creation and reporting. Acceptance: aggregation calculations are correct for sample datasets and edge cases; tests run green in CI."

## Overview

This feature establishes the **data contracts** for the personal finance tracker: the core domain concepts (transactions, categories, transaction direction, category compatibility) and the request/response shapes that the API will use to exchange data with consumers. It is a foundation-layer feature — no behavior (storage, aggregation, export, validation enforcement) is delivered here. Subsequent features (in-memory repositories, transactions/categories endpoints, period/weekly report strategy, JSON/CSV export) build on top of these contracts.

The feature scope is intentionally narrow: define the *shape* of the data so downstream slices can be implemented and tested independently against a stable, immutable contract.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Represent a transaction with one-or-more categories, direction, timestamp, and amount (Priority: P1)

The system must be able to describe a single income or expense entry the user has made. Every transaction has a full timestamp (year, month, day, hour, minute, second), a description, a positive amount, a direction (income or expense), a stable integer identifier so it can be referenced later (e.g., deletion, reporting), and a non-empty collection of category references — a single transaction MAY be tagged with multiple categories (e.g., a grocery run could be both "Groceries" and "Health Food").

**Why this priority**: A "transaction" is the atomic unit of the entire product. No report, no listing, no export can exist until a transaction can be unambiguously described. Without this contract, no other feature can be built or tested.

**Independent Test**: Verify in isolation by constructing a transaction value with all required attributes (including both the single-category and multi-category cases), confirming each attribute is exposed exactly as supplied, and confirming that two transactions with identical attributes but different identifiers are treated as distinct.

**Acceptance Scenarios**:

1. **Given** a valid set of transaction attributes (integer identifier, timestamp, description, positive amount, direction, a non-empty collection of category references), **When** a transaction value is constructed, **Then** every attribute is exposed exactly as supplied with no loss of precision on the monetary amount and second-level precision on the timestamp.
2. **Given** two transactions with the same timestamp, description, amount, direction, and category references but different identifiers, **When** they are compared, **Then** they are recognized as two distinct transactions.
3. **Given** a transaction's monetary amount, **When** the value is inspected, **Then** it carries no sign — the direction attribute alone conveys whether it is income or expense.
4. **Given** a transaction tagged with two categories (e.g., category 2 "Groceries" and category 5 "Health Food"), **When** the transaction value is inspected, **Then** both category references are exposed as an immutable collection in the order supplied.

---

### User Story 2 - Represent a category with name and supported direction (Priority: P1)

The system must be able to describe a category (e.g., Groceries, Salary, Utilities) that groups transactions. Each category has a stable identifier, a name, and a declared compatibility — whether it supports income transactions, expense transactions, or both. Category name comparisons must be case-insensitive so that "Groceries" and "groceries" cannot both exist as separate categories.

**Why this priority**: Categories are the second pillar of the model. The product's main value (weekly summaries with category breakdowns) is impossible without them, and transactions cannot be assigned without a category contract to point at.

**Independent Test**: Verify by constructing a category value with each of the three compatibility values (income-only, expense-only, both) and confirming each attribute is exposed exactly as supplied. Verify case-insensitive name equivalence by comparing two names that differ only in casing.

**Acceptance Scenarios**:

1. **Given** a valid category (identifier, name, compatibility), **When** the value is constructed, **Then** all three attributes are exposed exactly as supplied.
2. **Given** a category compatibility of "Both", **When** the category is paired with either an income or an expense transaction, **Then** the pairing is recognized as compatible.
3. **Given** two category names "Groceries" and "groceries", **When** they are compared for equivalence, **Then** they are treated as the same name.

---

### User Story 3 - Submit a new transaction through a stable API request shape (Priority: P1)

An API consumer must be able to submit a new transaction using a request shape that carries only the attributes the consumer is responsible for supplying (timestamp, description, amount, direction, and a non-empty collection of category references). The shape does not include the server-assigned identifier. It uses field names that are stable, predictable, and serialization-friendly.

**Why this priority**: This is the on-ramp for every other product capability. The MVP brief lists "add transactions" as the first user action; the request shape is the contract every downstream piece (validation, storage, reporting) depends on.

**Independent Test**: Verify by serializing the request shape from a sample payload that matches the README's example (`timestamp`, `description`, `amount`, `transactionType`, `categoryIds`) and confirming round-trip fidelity (serialize → deserialize → re-serialize produces the same payload).

**Acceptance Scenarios**:

1. **Given** the sample request payload from the README (`{ "timestamp": "2026-05-07T09:15:00", "description": "Coffee", "amount": 4.50, "transactionType": "Expense", "categoryIds": [3] }`), **When** it is bound to the request shape, **Then** every field is captured with the expected name and type.
2. **Given** a request shape value, **When** it is mutated after construction, **Then** the operation is impossible — the shape is immutable.
3. **Given** a request payload where the monetary amount has two decimal places, **When** it is bound and re-emitted, **Then** the two-decimal precision is preserved.

---

### User Story 4 - Receive transaction and category data in a stable, predictable response shape (Priority: P2)

When the API returns a transaction, the response must include the server-assigned identifier and an inline collection of category summaries (each carrying at least the category's identifier and name), so that the consumer does not need a second round-trip to display a meaningful row. When the API returns a category, the response must include identifier, name, and supported direction. Response shapes must be distinct from request shapes (no shared mutable type) so the contract can evolve in either direction without breaking the other.

**Why this priority**: Responses are visible to the consumer immediately after every successful API call. A stable response shape protects consumers from churn and is reused inside larger response envelopes (e.g., the report response includes a list of transactions).

**Independent Test**: Verify by constructing a response shape from a sample transaction tagged with one or more categories (matching the README's example response), then confirming the JSON output exactly matches the README's documented format (field names, nesting, casing, ordering inside the `categories` collection).

**Acceptance Scenarios**:

1. **Given** the sample response payload from the README, **When** the response shape is serialized, **Then** the produced JSON matches the README's example field-for-field (including the nested `categories` collection of objects each with `id` and `name`).
2. **Given** a transaction response shape value, **When** it is inspected, **Then** the monetary amount is positive and the transaction direction is exposed explicitly.
3. **Given** a category response shape value, **When** it is inspected, **Then** the supported direction is one of three documented values and nothing else.
4. **Given** a transaction tagged with multiple categories, **When** the response is serialized, **Then** every category's identifier and name appears in the `categories` collection in the order they are attached to the transaction.

---

### User Story 5 - Return a report as a summary with totals and a per-category breakdown (Priority: P2)

When the API returns a generated report, the response is an **aggregated summary** — not a list of transactions. It exposes:

- the report type (echoing the request's `ReportType` enum),
- a short string descriptor of the resolved period (`"2026-W20"` for an ISO-week report; `"2026-05-01..2026-05-31"` for a custom-period report),
- `incomeTotal` (sum of amounts on `Income` transactions in the period, positive),
- `expenseTotal` (sum of amounts on `Expense` transactions in the period, positive),
- `netTotal` (= `incomeTotal − expenseTotal`, may be negative),
- a `categoryBreakdown` collection in which each item names a category and gives a single **signed** total (positive for amounts that came in via that category, negative for amounts that went out via it).

The response **does not include the contributing transactions themselves**. A consumer that needs the underlying transaction list calls the transactions endpoint with a date filter separately.

**Why this priority**: This is the MVP's headline deliverable — "weekly totals and category breakdowns" per the task brief. The response shape is the contract every report strategy (period, ISO-week, month, …) emits and that any future export feature consumes. P2 because it depends on Stories 1 and 2 being defined first.

**Independent Test**: Verify by constructing a report response from a sample set of matching transactions and confirming the JSON output exactly matches the README's documented format — `type`, `period`, three totals, and an ordered `categoryBreakdown` collection.

**Acceptance Scenarios**:

1. **Given** the README's example report response (ISO-week 2026-W20 with one income transaction and four expense transactions), **When** the report response shape is serialized, **Then** the produced JSON matches the README's example field-for-field (`type`, `period`, the three totals, and the five-item `categoryBreakdown` in the documented order with the documented signed totals).
2. **Given** a report whose date range matches no transactions, **When** the response is serialized, **Then** `incomeTotal`, `expenseTotal`, and `netTotal` are all exactly `0`, and `categoryBreakdown` is an empty collection (never null).
3. **Given** a report response value, **When** the `categoryBreakdown` collection is inspected, **Then** it is read-only / immutable.
4. **Given** a single transaction tagged with two categories (e.g., a $20 grocery run tagged both "Groceries" and "Health Food"), **When** the report is generated for the containing period, **Then** the transaction's signed amount appears in *both* category breakdown lines — once under "Groceries" and once under "Health Food".
5. **Given** a period in which transactions sum to `incomeTotal = $1,200` and `expenseTotal = $430.50`, **When** the breakdown is inspected, **Then** `categoryBreakdown` is ordered with income (positive-total) categories first and expense (negative-total) categories second, with category-name-ascending order applied within each of those two groups.

---

### User Story 6 - Submit a report request with a typed report type and a per-type data payload (Priority: P3)

An API consumer must be able to submit a report request using an envelope that carries (a) a report type from a closed enum (`Period`, `IsoWeek`, …) and (b) a `data` object whose fields are defined per report type.

**MVP scope (revised 2026-05-21)**: only the `Period` report type has a defined `data` payload shape in this feature. `IsoWeek` is retained in the `ReportType` enum as a *documented future value* so the README's `ReportResult` example can serve as the US5 test fixture, but its `data` payload DTO (`IsoWeekReportData`) and its strategy ship in a later feature. The acceptance scenarios below cover only the `Period` flow plus the unknown-enum-value rejection path.

- For `Period` reports, `data` has two calendar dates: `start` and `end` (inclusive on both ends).
- For `IsoWeek` reports, `data` has a single string field `week` in ISO 8601 form (e.g., `"2026-W19"`) — *shape defined and tested when the IsoWeek strategy ships*.

**Why this priority**: The MVP only needs the `Period` report, so the typed `data` envelope is partly forward-looking. P3 because the period report could ship with a typed `start`/`end` request directly. However, the enum + per-type `data` shape is what the README and downstream features require for future report types, so it is in scope.

**Independent Test**: Verify by binding the README's example request (`{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }`) into the report request shape, confirming the report type is exposed as the enum value `Period` and the `data` payload is exposed as a typed value with `start` and `end` calendar-date fields.

**Acceptance Scenarios**:

1. **Given** the README's example `Period` request, **When** it is bound to the request shape, **Then** the report type is exposed as the enum value `Period` and the `data.start` and `data.end` fields are exposed as calendar dates.
2. **Given** a `Period` request where `start` is later than `end`, **When** it is bound to the request shape, **Then** the binding succeeds (the shape itself does not enforce range validity — that is downstream validation), but the field values are preserved exactly as supplied so validation can reject them.
3. **Given** a request whose `type` value is unknown to the enum (e.g., `"Quarter"`, `"Month"`), **When** it is bound to the request shape, **Then** the binding fails with a clear error — only the closed set of enum values (`Period`, `IsoWeek`) is accepted.

---

### Edge Cases

- A transaction is logged at a boundary moment of a week — e.g., `2026-05-03T23:59:59` (Sunday) versus `2026-05-04T00:00:00` (Monday). The timestamp is precise to the second, so any downstream aggregation can bucket the transaction unambiguously by its date component.
- Two transactions are logged within the same minute but a different number of seconds apart — both are preserved with distinct timestamps; the contract never silently rounds time precision.
- A transaction is tagged with a single category whose compatibility is "Both" — the model permits pairing with either an income or an expense direction.
- A transaction is tagged with multiple categories — every attached category must be compatible with the transaction's direction (validation enforced downstream). The contract itself allows any non-empty collection of category references; rejecting incompatible combinations is a later feature.
- A transaction is tagged with the same category twice (duplicate identifier in the collection) — the contract permits it; deduplication is a downstream validation concern, not a structural concern.
- A category name contains a non-ASCII character (e.g., "Café") — the value is preserved exactly and case-insensitive equivalence still functions across Unicode forms commonly used in user input.
- A monetary amount uses high-precision decimal input (e.g., 32.105) — the model preserves the precision the consumer supplied; rounding decisions are deferred to display/serialization rules in a later feature.
- A report response describes a period in which no transactions match — `incomeTotal`, `expenseTotal`, and `netTotal` are all `0` and `categoryBreakdown` is an empty collection (never null, never absent).
- A `Period` report whose `start` equals `end` — the period covers exactly one calendar day; the contract makes no distinction between a one-day period and a multi-day period.
- An `IsoWeek` request — deferred. With `IsoWeekReportData` out of scope in this feature, the envelope binding for `{ "type": "IsoWeek", … }` succeeds (it carries `Type = ReportType.IsoWeek` and an unparsed `Data` `JsonElement`), but no strategy can consume it. Validation of the `data.week` payload string (e.g., rejecting `"2026-W99"`, `"not-a-week"`) lands together with the strategy.
- A transaction tagged with multiple categories appears in **each** of those categories' breakdown lines with its full signed amount. As a result, the arithmetic sum of `categoryBreakdown[*].total` MAY exceed (in absolute value) the `netTotal` — this is expected, not a defect, and is documented in the Assumptions section.
- A `Both`-type category in the period contains a mix of `Income` and `Expense` transactions — the category's single breakdown total nets them (income contributions add positive, expense contributions add negative). A category whose income and expense fully offset has a `total` of `0` but still appears as a row in `categoryBreakdown`.
- A consumer requests the same report twice with identical parameters and unchanged transaction data — both responses are byte-identical; no cached or persisted report result exists between the two calls.

## Requirements *(mandatory)*

### Functional Requirements

**Domain entity contracts**

- **FR-001**: The system MUST define a transaction entity contract with the following attributes: unique integer identifier, timestamp (full datetime to second-level precision — year, month, day, hour, minute, second), required non-empty description, positive monetary amount, transaction direction (income or expense), and a **non-empty, read-only collection of integer category references** (a transaction may belong to one or more categories).
- **FR-002**: The transaction entity contract MUST treat the monetary amount as an unsigned value; direction is conveyed solely by the transaction direction attribute.
- **FR-003**: The system MUST define a category entity contract with the following attributes: unique integer identifier, required non-empty name, and a compatibility attribute taking exactly one of three values: income-only, expense-only, or both.
- **FR-004**: The system MUST define a transaction direction concept as an enum with exactly two documented values: "Income" and "Expense". It is an attribute of transactions, not a standalone entity.
- **FR-005**: The system MUST define a category compatibility concept as an enum with exactly three documented values: "Income", "Expense", and "Both". It is an attribute of categories, not a standalone entity.
- **FR-006**: The system MUST define a report type concept as an enum. The enum declares two values — `"Period"` and `"IsoWeek"` — but **only `"Period"` has a defined `data` payload DTO in this feature**; `"IsoWeek"` is retained as a documented future value whose payload DTO and strategy ship in a later feature (see the MVP scope note on User Story 6). Future report types extend the enum without changing the request envelope shape. It is an attribute of the report request, not a standalone entity.
- **FR-007**: Category name comparisons MUST be case-insensitive — "Groceries" and "groceries" are treated as the same name for any equivalence purpose.
- **FR-008**: All domain entity contracts MUST be immutable once constructed — there is no public mutator for any attribute, and the transaction's category-reference collection MUST be exposed as a read-only / immutable collection.

**Request shapes / DTOs (API consumer → system)**

- **FR-009**: The system MUST define a transaction create request DTO exposing exactly: timestamp, description, amount, transaction direction, and a non-empty collection of integer category references. It MUST NOT expose a transaction identifier (the system assigns it).
- **FR-010**: The system MUST define a category create request DTO exposing exactly: name and compatibility. It MUST NOT expose a category identifier (the system assigns it).
- **FR-011**: The system MUST define a report request DTO that is an envelope of exactly two fields: a report type (the `ReportType` enum) and a typed `data` payload whose concrete shape is determined by the report type. The envelope itself MUST NOT carry the per-type fields directly — different report types use different `data` shapes.
- **FR-012**: The system MUST define a `Period` report data DTO exposing exactly two fields: `start` (calendar date) and `end` (calendar date). Both bounds are inclusive in the period the report covers; the contract itself does not enforce `start ≤ end` (that is downstream validation).
- ~~**FR-013**: The system MUST define an `IsoWeek` report data DTO exposing exactly one field: `week` (string in ISO 8601 form such as `"2026-W19"`).~~ **Deferred (2026-05-21)** — `IsoWeekReportData` ships in the later IsoWeek-strategy feature. This feature does not define the type; `ReportRequest.Data` for an `IsoWeek` request is preserved as an unparsed `JsonElement` and not validated at the envelope layer.
- **FR-014**: All request DTOs MUST be immutable once constructed.

**Response shapes / DTOs (system → API consumer)**

- **FR-015**: The system MUST define a transaction response DTO exposing: integer identifier, timestamp, description, amount (positive), direction, and a read-only collection of inline category summaries (each summary containing at minimum the category's integer identifier and name) preserving the order in which categories are attached to the transaction.
- **FR-016**: The system MUST define a category response DTO exposing: integer identifier, name, and compatibility.
- **FR-017**: Request DTOs and response DTOs for the same entity MUST be distinct types — they MAY share attribute names but MUST NOT share an underlying type that allows one to be used in place of the other.

**Report response shape**

- **FR-018**: The system MUST define a `ReportResult` DTO with exactly the following fields:
  - `type` — the `ReportType` enum value that produced this result (echoed from the request);
  - `period` — a short string descriptor of the resolved coverage (`"yyyy-Www"` for ISO-week results, `"yyyy-MM-dd..yyyy-MM-dd"` for period results — future report types document their own descriptor format);
  - `incomeTotal` — the sum of `Income`-direction transaction amounts in the period, exposed as positive-or-zero;
  - `expenseTotal` — the sum of `Expense`-direction transaction amounts in the period, exposed as positive-or-zero;
  - `netTotal` — `incomeTotal − expenseTotal`, which MAY be negative;
  - `categoryBreakdown` — a read-only / immutable collection of `CategoryBreakdownItem`.
  The DTO MUST NOT carry a `currency` field, a list of contributing transactions, or any other report metadata.
- **FR-019**: The system MUST define a `CategoryBreakdownItem` DTO with exactly two fields: `category` (the category's display name) and `total` (a **signed** decimal — positive when the category's contribution to the period is income-side, negative when expense-side). The breakdown item carries no separate direction field (the sign of `total` is the direction indicator) and no transaction count.
- **FR-020**: A transaction tagged with N categories MUST contribute its full signed amount to **each** of those N categories' breakdown items (i.e., transactions are *not* split or pro-rated across their categories). For a `Both`-type category whose attached transactions span both directions in the period, the single breakdown total nets the income and expense contributions. As a documented consequence: the arithmetic sum of `categoryBreakdown[*].total` is not a meaningful number when any transaction in the period has multiple categories — it MAY exceed `netTotal` in absolute value.
- **FR-021**: `categoryBreakdown` MUST be ordered (a) by sign of `total` (positive — income side — first, then negative — expense side), then (b) by category name ascending within each group. Categories whose `total` is exactly `0` (only possible for `Both`-type categories whose income and expense fully offset) sort with the income-side group.
- **FR-022**: When the report describes a period with no matching transactions, `incomeTotal`, `expenseTotal`, and `netTotal` MUST all be exactly `0` and `categoryBreakdown` MUST be an empty collection (never null, never omitted).

**Entity ↔ DTO relationship and mapping**

- **FR-023**: For the MVP, every transaction/category request/response DTO MAY carry the same attribute set as its corresponding domain entity (no extra, missing, or renamed attributes). They MUST still be defined as distinct types so the layer boundary is preserved against future divergence. (`ReportResult` and `CategoryBreakdownItem` have no corresponding domain entities — they are pure aggregation contracts produced by report strategies.)
- **FR-024**: Translation between domain entities and DTOs MUST go through dedicated mapping functions (one per type pair, **all in the Business layer**). The MVP mappers are trivial 1:1 field copies; they exist so consumers do not accept a domain entity in place of a DTO or vice versa. Report aggregation (filtering transactions by the resolved date range, computing totals, building the breakdown, sorting it) is also a Business-layer concern; the API layer never sees a domain entity.
- **FR-025**: Report results MUST be computed ad-hoc from current entity state on every request. The system MUST NOT persist, cache, or otherwise store generated report results.

**Serialization compatibility**

- **FR-026**: Every request and response DTO MUST round-trip losslessly through the project's chosen JSON serialization, preserving: all attribute names, all types, decimal precision on monetary amounts, second-level precision on transaction timestamps, the `ReportType` enum as its name (e.g., `"Period"`, `"IsoWeek"`), the sign of `CategoryBreakdownItem.total`, and the order of items in any read-only collection (categories on a transaction, items in `categoryBreakdown`).
- **FR-027**: The serialized field names for the transaction response and the report response MUST match the README's documented example payloads verbatim (including casing, nesting, the use of `categories` on `TransactionResponse`, and the structure of `categoryBreakdown` on `ReportResult`), so the README continues to function as the canonical contract reference.

**Out of scope (explicitly)**

- **FR-028**: This feature MUST NOT implement storage of any entity (no repositories, no seeding).
- **FR-029**: This feature MUST NOT implement validation enforcement at API boundaries (no controllers, no model-binding validation messages, no rejection of bad input). The shapes encode *what is valid*; enforcement is a later feature. Specifically: rejecting a `Period` whose `start > end`, an unparseable `IsoWeek.week` string, an empty category-reference collection, a duplicate category in the collection, or a category whose compatibility does not match the transaction's direction is *not* in scope here.
- **FR-030**: This feature MUST NOT implement report-strategy execution (resolving `data` into a date range, filtering transactions, computing totals, building the breakdown) or export. Only the contracts those features consume and produce, plus the entity↔DTO mappers, are in scope here.

### Key Entities

Domain entities (live in the Data layer):

- **Transaction**: A single income or expense entry. Carries an integer identifier, a full timestamp (year, month, day, hour, minute, second), description, positive monetary amount, a direction value (`Income` or `Expense`), and a **non-empty, read-only collection of integer category references** (a transaction may be tagged with one or more categories).
- **Category**: A grouping label for transactions. Carries an integer identifier, name, and a compatibility value (`Income`, `Expense`, or `Both`).

Enums (attributes on the entities or request envelopes above; **not** standalone entities):

- **TransactionType** — closed set: `Income`, `Expense`. Used on transactions.
- **CategoryType** — closed set: `Income`, `Expense`, `Both`. Used on categories.
- **ReportType** — closed set: `Period`, `IsoWeek` (extensible). Used on `ReportRequest` to discriminate the `data` payload's shape. **In this feature** only `Period` has a defined payload DTO; `IsoWeek` is reserved (see MVP scope note on US6).

DTOs (live in the Business layer; the API layer reuses them and only adds API-specific models when there is a genuine need):

- **TransactionCreateRequest**: The DTO an API consumer submits to create a transaction. Carries timestamp, description, amount, direction, and a non-empty collection of category references. Does not carry an identifier.
- **CategoryCreateRequest**: The DTO an API consumer submits to create a category. Carries name and compatibility. Does not carry an identifier.
- **TransactionResponse**: The DTO returned for a transaction. Carries identifier, timestamp, description, amount, direction, and a read-only collection of inline category summaries (each: identifier + name) in the order the categories are attached.
- **CategoryResponse**: The DTO returned for a category. Carries identifier, name, and compatibility.
- **ReportRequest**: The envelope DTO a consumer submits to request a report. Two fields only: `type` (`ReportType` enum) and `data` (a typed payload whose shape is determined by `type`).
- **PeriodReportData**: The `data` shape used when `type = Period`. Two fields: `start` (calendar date), `end` (calendar date).
- ~~**IsoWeekReportData**~~ — **deferred to a later feature**. The `data` shape used when `type = IsoWeek` (one string field `week`, ISO 8601 week format) is documented for context but is not implemented in this feature.
- **CategoryBreakdownItem**: A per-category summary row inside a `ReportResult`. Two fields: `category` (category display name) and `total` (signed decimal — positive for income contribution, negative for expense contribution; the sign carries the direction so there is no separate direction field). Carries no transaction count.
- **ReportResult**: The aggregated DTO returned by any report strategy. Carries `type` (`ReportType` enum), `period` (string descriptor of the resolved coverage — `"yyyy-Www"` for ISO-week, `"yyyy-MM-dd..yyyy-MM-dd"` for custom period), `incomeTotal`, `expenseTotal`, `netTotal`, and a read-only `categoryBreakdown` collection of `CategoryBreakdownItem` ordered income-first then expense-second, alphabetical within each. Does **not** carry the underlying transactions.

Mappers (live alongside the DTOs in the Business layer):

- **TransactionMapper / CategoryMapper**: Trivial 1:1 functions that translate between the corresponding domain entity and the matching request/response DTO (including the multi-category collection on transactions). Their purpose is to preserve the layer boundary, not to transform data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every entity and DTO described in `README.md`'s "Core Domain", "Period report example" request, and "Period report example" response sections is represented in this feature's contracts with field-for-field parity (verified by serializing each contract from the README's example payloads and comparing the result to the README example).
- **SC-002**: A downstream feature can be drafted (storage, validation, aggregation, or export) without proposing a single new attribute on any contract defined here — the contracts cover 100% of the attributes the MVP brief and README require.
- **SC-003**: Every request and response DTO round-trips through JSON serialization with zero loss of fidelity — including decimal precision on monetary amounts, second-level precision on transaction timestamps, `ReportType` enum name preservation, the sign of `CategoryBreakdownItem.total`, and order preservation in any read-only collection (categories on a transaction, items in `categoryBreakdown`) — when given any of the example payloads in the README.
- **SC-004**: Every domain entity and every DTO is immutable — any attempt to mutate a constructed value after creation is impossible by construction (no public setters, no exposed mutable collections).
- **SC-005**: A developer unfamiliar with the project, given only `README.md` and this spec, can identify every attribute that participates in the transaction-create → report-generate → JSON-export flow within 10 minutes of reading.
- **SC-006**: Every report response is computed fresh on every request — running the same report request twice with no entity changes in between produces byte-identical results, and no report-related state is observable in storage between the two calls.

## Assumptions

- **Stakeholder-stated implementation preferences** (captured because they materially constrain the achievable design):
  - DTOs and domain entities are defined as **records** (immutable, value-based equality) — this is what makes the immutability requirements (FR-008 for domain entities, FR-014 for request DTOs, FR-018's read-only `categoryBreakdown`) achievable by construction.
  - **Integer identifiers** are used for both transactions and categories. This intentionally replaces the GUID-style identifiers shown in the README's existing example payloads; the README is being updated alongside this spec.
- **Layer placement**:
  - Domain entities and the two enums (`TransactionDirection`, `CategoryCompatibility`) live in the **Data layer**.
  - All DTOs — both HTTP request/response shapes (transactions, categories) and report contracts — live in the **Business layer**.
  - The **API layer** reuses the Business-layer DTOs directly and only introduces API-specific models if/when there is a concrete need (e.g., an API-versioning wrapper). None are needed for the MVP.
  - **Mappers** between entities and DTOs live in the Business layer, next to the DTOs they target.
- **Currency**: Implicit USD. No currency field appears on any entity, DTO, or response. Multi-currency support is out of scope for the MVP.
- **Date / time precision**:
  - **Transactions** are stamped with a full local datetime to the second (year, month, day, hour, minute, second). No timezone offset is stored — the single-user MVP assumes the user's local clock.
  - **Report period bounds** (`from`/`to`) remain calendar-day, even though transactions carry full timestamps. The period is interpreted as `[from 00:00:00, to 23:59:59]` inclusive in the user's local clock when the strategy aggregates.
- **Reports are ad-hoc**: every report response is computed fresh from current entity state on every request. The system does not persist, cache, or otherwise store generated report results. There is no report-history endpoint, no report identifier, and no replay-of-a-prior-report concept (separate from the MCP-style replay layer described in `README.md`, which replays *context*, not stored report results).
- **Report response is a summary, not a transaction list**: the `ReportResult` carries `incomeTotal`, `expenseTotal`, `netTotal`, and a `categoryBreakdown` collection. It does **not** return the underlying transactions — a consumer that needs them calls the transactions endpoint with a date filter separately. This is the matching aggregation deliverable the task brief calls out ("weekly totals and category breakdowns").
- **Multi-category attribution to the breakdown**: a transaction tagged with N categories contributes its full signed amount to each of those N `categoryBreakdown` lines (full-attribution, not pro-rated). The trade-off, documented in FR-020 and in the edge cases, is that `categoryBreakdown[*].total` does not arithmetically sum to `netTotal` when any transaction in the period is multi-tagged. Pro-rating was rejected because it loses information (a $20 grocery run tagged with two categories has spent $20 on each in a meaningful sense, not $10) and because the user's literal illustration showed full per-category totals.
- **`ReportType` is an enum**: the closed set for the MVP is `Period`, `IsoWeek`. Adding a new report type (e.g., `Month`) extends the enum and adds a new `*ReportData` DTO; the envelope shape and the report result shape do not change. Unknown enum values are rejected at the binding layer.
- **Multi-category compatibility rule** (enforced downstream, not by this contract): when a transaction has more than one category, **every** attached category must be compatible with the transaction's direction. So an `Expense` transaction MAY be tagged with any combination of `Expense`-compatibility and `Both`-compatibility categories but MUST NOT be tagged with any `Income`-only category. The contract here permits any non-empty collection; rejection is a later validation feature.
- **Monetary precision** is whatever fixed-point decimal precision the chosen technology supports natively — sufficient for two-decimal currency and tolerant of higher-precision inputs for forward compatibility. No specific currency-rounding rule is encoded into the entity contract.
- **No validation logic** is encoded into the shapes themselves (e.g., the `Amount` attribute on the transaction entity is typed to accept any value its underlying numeric type permits; rejection of zero or negative amounts is a downstream concern). The shapes describe *what is valid*; enforcement is delivered separately.
- **Report response field names** in the README example use camelCase (`incomeTotal`, `expenseTotal`, `categoryBreakdown`, `transactionType`). The serialization configuration that produces this casing is assumed to already exist in `Finance.Api` (see the `AddOpenApi()` setup) or to be added as a trivial configuration step; the DTO definitions themselves use the language's idiomatic naming and rely on the serializer for the wire format.
- **`ReportRequest.Parameters` shape**: The parameters payload is structured (a parsable value), not an opaque string. The exact structured type (e.g., a generic JSON node, a dictionary, etc.) is an implementation decision deferred to planning, provided it supports per-strategy parsing as described in the period report spec.
- **No persistence contract** is defined here. Repository interfaces are deferred to the next feature (the in-memory repository slice).
- **Out-of-band note**: The two existing AI-artifact specs (`ai-artifacts/Specifications/in-memory-repository-spec.md`, `ai-artifacts/Specifications/period-report-strategy-spec.md`) still describe GUIDs, day-level `DateOnly` transaction dates, and a `currency` field on the report result. Those documents predate this design refresh and are out of date relative to this spec, `README.md`, and `CLAUDE.md`. They will be reconciled when their corresponding features are scheduled.
