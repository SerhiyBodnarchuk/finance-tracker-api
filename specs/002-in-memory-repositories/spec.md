# Feature Specification: Seeded In-Memory Repositories

**Feature Branch**: `002-in-memory-repositories`

**Created**: 2026-05-21

**Status**: Draft

**Input**: User description: "Implement repositories with pre seeded data. Examples etc can be used from ai-artifacts/Specifications/in-memory-repository-spec.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Demo data available immediately on app start (Priority: P1)

A developer or evaluator launches the API on a fresh machine, makes their first request to list transactions, and sees a useful set of pre-populated transactions and the categories they belong to — without configuring a database, running migrations, or seeding manually.

**Why this priority**: This is the load-bearing premise of the MVP. Until the storage layer can return useful data deterministically on startup, no downstream feature (reports, exports, the MCP layer) can be demonstrated end-to-end. It is also the prerequisite that unblocks every later feature that consumes the repository layer.

**Independent Test**: Start the API on a clean checkout, query the repository layer through its public surface for the full transaction and category collections, and verify both contain the documented seed records with stable identifiers.

**Acceptance Scenarios**:

1. **Given** the API has just started for the first time, **When** the transaction repository is asked for all transactions, **Then** it returns the five seeded transactions in the documented amounts, dates, types, and category associations.
2. **Given** the API has just started for the first time, **When** the category repository is asked for all categories, **Then** it returns the five seeded categories with the documented names and category types.
3. **Given** seed records are present, **When** the same record is requested by identifier across two consecutive API restarts, **Then** the identifier resolves to the same logical record both times (deterministic seed IDs).

---

### User Story 2 - Save new transactions and categories within an app lifetime (Priority: P2)

A consumer of the repository layer (a controller or business-layer service in a later feature) adds a new transaction or a new category at runtime and expects it to be visible to every subsequent request handled by the same running process, without re-reading the seed file or losing other recent writes.

**Why this priority**: Read-only seed data alone is not enough to demonstrate the period-report and export features later. The repository must accept writes and retain them in process memory so the API behaves like a real CRUD surface between restarts.

**Independent Test**: Call `Add` on a repository with a valid record, then immediately call `GetAll` and `GetById` from the same process, and verify the new record appears with a freshly assigned identifier distinct from all existing identifiers.

**Acceptance Scenarios**:

1. **Given** the seeded transaction set, **When** a caller adds a new valid transaction, **Then** the returned transaction has a non-zero identifier that does not collide with any seeded or previously added identifier, and a subsequent `GetAll` includes it.
2. **Given** the seeded category set, **When** a caller adds a new category whose name is not already present (compared case-insensitively), **Then** the category is added and visible to subsequent reads.
3. **Given** the seeded category set, **When** a caller attempts to add a category whose name matches an existing one ignoring case (e.g., "groceries" vs. "Groceries"), **Then** the add is rejected without mutating the underlying store.
4. **Given** a transaction or category was added at runtime, **When** the host process restarts, **Then** the runtime addition is gone and only the original seeded records remain — restart-on-reset is expected behavior, not a defect.

---

### User Story 3 - Look up and remove transactions by identifier (Priority: P3)

A consumer needs to fetch a single transaction by its identifier (for example, to confirm a write before showing it back to a caller, or to support a future delete endpoint) and to remove a transaction they previously inserted.

**Why this priority**: Single-record lookup and deletion round out the minimum CRUD surface the controllers and report features will need. They are simpler than create and follow naturally once create works; they are P3 because no end-to-end demo strictly requires them, but they prevent the storage layer from leaking only an "append-only" abstraction.

**Independent Test**: Add a transaction, fetch it by the returned identifier and verify equality, delete it by that identifier, then fetch again and verify it is gone; finally, attempt to delete the same identifier a second time and verify the call cleanly reports "not found".

**Acceptance Scenarios**:

1. **Given** a transaction exists in the store, **When** a caller asks for it by its identifier, **Then** the repository returns that exact record.
2. **Given** no transaction exists with a given identifier, **When** a caller asks for it by that identifier, **Then** the repository returns "not found" rather than throwing or returning a placeholder.
3. **Given** a transaction exists in the store, **When** a caller deletes it by identifier, **Then** the call reports success and a subsequent read by the same identifier reports "not found".
4. **Given** no transaction exists with a given identifier, **When** a caller deletes by that identifier, **Then** the call reports "not found" without mutating the store and without throwing.

---

### Edge Cases

- A category lookup is asked about an identifier that was never issued — the repository must answer "not found" rather than fault. Categories have no delete operation in this scope by design.
- A transaction is added whose category-id list references one or more identifiers that do not exist in the category store. This violates a documented domain invariant, but enforcement of that invariant lives outside the repository in a later feature; the repository itself stores what it is given and is not the boundary that prevents it. The spec's tests cover the uniqueness invariant the repository *does* own (category names) and leave referential-integrity tests to the business-layer feature.
- A duplicate-name category is rejected and the caller retries with a different name — the rejected attempt must not have consumed an identifier from the issuing sequence (no "hole" in the id space caused by a failed add).
- Two callers add transactions back-to-back from the same process — every assigned identifier is unique across the seed and across all prior runtime additions in this process; ordering of `GetAll` is the insertion order (seeded records first, runtime additions appended).
- All amounts are USD; no currency conversion or multi-currency handling is in scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The transaction store MUST be populated with the documented seed transactions before the first incoming request is handled, so that consumers see useful data without any setup step.
- **FR-002**: The category store MUST be populated with the documented seed categories before the first incoming request is handled, and every seeded transaction's category references MUST resolve to a seeded category.
- **FR-003**: Seeded records MUST use deterministic identifiers across runs so that tests and demo flows can rely on specific identifier values (for example, "the seeded Salary category is always category #1").
- **FR-004**: Identifiers issued for runtime additions MUST be unique across the seed set and across all previously issued runtime identifiers within the same process — no collisions, no reuse of a freed identifier, no holes caused by rejected adds.
- **FR-005**: The transaction store MUST expose, at minimum, the ability to list all transactions, fetch one by identifier, add a new one (returning the stored form with its assigned identifier), and delete one by identifier reporting success or "not found" without throwing.
- **FR-006**: The category store MUST expose, at minimum, the ability to list all categories, fetch one by identifier, and add a new one (returning the stored form with its assigned identifier). Categories have no delete operation in this scope.
- **FR-007**: Adding a category whose name matches an existing category's name ignoring case MUST be rejected; the underlying store MUST NOT be mutated by a rejected add.
- **FR-008**: Writes accepted within a process MUST remain visible to every subsequent read within that same process until the process exits, including across many requests over the lifetime of the app.
- **FR-009**: When the process is restarted, the store MUST return to the original seeded contents — runtime writes are expected to be lost. This is documented intentional behavior for the MVP, not a defect.
- **FR-010**: A read for a non-existent identifier MUST return a clearly distinguishable "not found" signal to the caller, not an exception and not a placeholder record.
- **FR-011**: The seed data MUST contain only safe, fake values — no real names, account numbers, emails, identifiers, or any other personally identifying or sensitive information.
- **FR-012**: The seeded category set MUST cover the income/expense mix required by the seeded transactions: at least one income-compatible category for the salary record, and expense-compatible categories for each expense seed.

### Key Entities

- **Category**: A label that classifies transactions. Carries a stable identifier, a human-readable name unique across the store ignoring case, and a category type that constrains which kinds of transaction may attach to it (income-only, expense-only, or either). Examples in the seed: Salary, Groceries, Transport, Entertainment, Utilities.
- **Transaction**: A single income or expense event. Carries a stable identifier, a timestamp (date with time-of-day precision in the user's local clock), a free-text description, an amount that is always greater than zero (direction is encoded in the type, not the sign), a transaction type (income or expense), and a non-empty list of identifiers referencing the categories it is tagged with. A transaction may be attached to one or more categories simultaneously.
- **Seed transaction set** (concrete contents — see Assumptions for the modelling of dates):
  - Salary, Income, 1200.00, "Monthly salary", on the 1st of the seed reporting month.
  - Transport, Expense, 14.20, "Uber Trip", on the 4th.
  - Groceries, Expense, 32.10, "Silpo Market", on the 5th.
  - Entertainment, Expense, 9.99, "Netflix Subscription", on the 6th.
  - Utilities, Expense, 65.00, "Electricity Bill", on the 10th.
- **Seed category set**: Salary (Income), Groceries (Expense), Transport (Expense), Entertainment (Expense), Utilities (Expense).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who clones the repository, builds it, and runs the API can retrieve the five seeded transactions and five seeded categories on the first request after startup, with no manual setup step in between (no database install, no migration command, no seeding script).
- **SC-002**: 100% of the documented acceptance scenarios for User Stories 1, 2, and 3 are covered by automated tests, and the full test suite for this feature runs to completion in under 5 seconds on a developer machine with no external service dependencies.
- **SC-003**: Across 10 consecutive cold starts of the same build, each seeded record resolves to the same identifier every time (deterministic seed identifiers, verified by test).
- **SC-004**: At least 100 transactions can be added to the in-memory store within a single process lifetime without any read operation exceeding 50 milliseconds — confirming the store is suitable for the demo and report workloads this MVP targets (this is not a production performance bar).
- **SC-005**: A duplicate-name category add (case-insensitive match against any existing name) is rejected in 100% of attempted cases and never causes a gap in the assigned identifier sequence.

## Assumptions

- The "seed reporting month" referenced by the seed transactions is May of the current modelled year. Concrete timestamps follow the example listing in `ai-artifacts/Specifications/in-memory-repository-spec.md` (2026-05-01, 2026-05-04, 2026-05-05, 2026-05-06, 2026-05-10). The time-of-day component of each seed timestamp is left to the implementation as long as all five fall inside their stated calendar day in the local clock — the report features that consume them treat inclusive-day windows, so any in-day time is acceptable.
- Seeded transactions each attach to exactly one category in the seed (the obvious one for that record). The store still represents the category attachment as a list to match the multi-category domain rule; the seed simply happens to contain single-element lists.
- All amounts are USD. No currency field is stored.
- Validation that lives above the storage boundary — for example, "amount must be greater than zero", "every referenced category exists", "the chosen categories are compatible with the chosen transaction type" — is **out of scope for this feature** and is owned by the business layer in a later feature. The repository's only documented invariant in this scope is category-name uniqueness (case-insensitive). This is a deliberate scope narrowing relative to the older ai-artifacts spec, which mixed business validation into the repository tests.
- API controllers, request/response DTOs, and HTTP endpoints are **out of scope for this feature**. The ai-artifacts spec lists HTTP endpoints (`GET /api/transactions`, etc.) for context, but wiring them up is a separate feature that depends on this one. This feature delivers only the storage layer plus dependency-injection wiring sufficient for the next feature to consume.
- Writes are accepted at single-process scope. There is no requirement to be safe under heavy concurrent traffic; the assumption is that the developer-demo workload is effectively serial and that no extra concurrency hardening beyond what the standard collection types already provide is needed for the MVP. If a later feature stresses this, the assumption will be revisited.
- Data loss on process restart is acceptable and is the documented intentional behavior for the MVP, not a defect to fix.
