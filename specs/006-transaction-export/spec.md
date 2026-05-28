# Feature Specification: Transaction Export (JSON / CSV)

**Feature Branch**: `006-transaction-export`

**Created**: 2026-05-28

**Status**: Draft

**Input**: User description: "Need to implement api endpoints to export transactions in json/csv based on content-negotiation header"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Export Transactions as JSON (Priority: P1)

A user wants a machine-readable dump of all transactions to feed into another tool or script. They request the export endpoint with an `Accept: application/json` header and receive a JSON array of all transactions.

**Why this priority**: JSON export is the simplest path and the format already used throughout the API — it delivers full value with minimal new behaviour.

**Independent Test**: Send `GET /api/transactions/export` with `Accept: application/json`; verify the response body is a JSON array containing every seeded transaction with all expected fields.

**Acceptance Scenarios**:

1. **Given** at least one transaction exists, **When** a request is made with `Accept: application/json`, **Then** the response has status 200, `Content-Type: application/json`, and a JSON array of all transactions.
2. **Given** no transactions exist, **When** a request is made with `Accept: application/json`, **Then** the response has status 200 and an empty JSON array `[]`.
3. **Given** a request with `Accept: application/json`, **When** the response is received, **Then** each transaction object contains `id`, `description`, `amount`, `type`, `timestamp`, and `categoryIds`.

---

### User Story 2 - Export Transactions as CSV (Priority: P2)

A user wants to open their transaction history in a spreadsheet application. They request the same export endpoint with `Accept: text/csv` and receive a well-formed CSV file they can save and open directly.

**Why this priority**: CSV is the dominant spreadsheet import format; without it the export feature has limited practical reach.

**Independent Test**: Send `GET /api/transactions/export` with `Accept: text/csv`; save the response body to a `.csv` file and verify it opens correctly in a spreadsheet with the expected columns and row count.

**Acceptance Scenarios**:

1. **Given** at least one transaction exists, **When** a request is made with `Accept: text/csv`, **Then** the response has status 200, `Content-Type: text/csv`, and a UTF-8 CSV body with a header row followed by one data row per transaction.
2. **Given** no transactions exist, **When** a request is made with `Accept: text/csv`, **Then** the response has status 200 and a CSV body containing only the header row.
3. **Given** a request with `Accept: text/csv`, **When** the response is received, **Then** the CSV columns are `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds` in that order, and `CategoryIds` values for multi-category transactions are represented as a semicolon-delimited list within the cell.
4. **Given** a transaction whose description contains a comma or double-quote, **When** exported as CSV, **Then** the description cell is correctly quoted per RFC 4180 so it parses as a single field.

---

### User Story 3 - Unsupported Format Rejection (Priority: P3)

A user (or misconfigured client) sends an `Accept` header for a format the API does not support. They receive a clear error rather than a garbled or empty response.

**Why this priority**: Graceful rejection prevents silent data loss and is testable in isolation.

**Independent Test**: Send `GET /api/transactions/export` with `Accept: application/xml`; verify the response is 406 Not Acceptable.

**Acceptance Scenarios**:

1. **Given** an `Accept` header with an unsupported media type, **When** the export endpoint is called, **Then** the response has status 406 Not Acceptable.
2. **Given** no `Accept` header (or `Accept: */*`), **When** the export endpoint is called, **Then** the response defaults to JSON (200 with `Content-Type: application/json`).

---

### Edge Cases

- What happens when a transaction's description contains commas, double-quotes, or newlines in a CSV export?
- How does the endpoint behave when the in-memory store is empty?
- What if the client sends `Accept: text/csv;q=0.5, application/json;q=1.0`? (quality-weighted preference — JSON should win)
- What if the `Accept` header lists multiple types, one supported and one not?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a dedicated export endpoint (`GET /api/transactions/export`) that returns all transactions in the requested format.
- **FR-002**: The system MUST support `application/json` as an export format, returning a JSON array of transaction objects.
- **FR-003**: The system MUST support `text/csv` as an export format, returning a UTF-8 CSV file with a header row.
- **FR-004**: The system MUST use HTTP content negotiation (the `Accept` request header) to determine the response format.
- **FR-005**: When the `Accept` header is absent or `*/*`, the system MUST default to JSON.
- **FR-006**: When the `Accept` header requests an unsupported media type and no fallback matches, the system MUST respond with HTTP 406 Not Acceptable.
- **FR-007**: CSV output MUST include the columns `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds` and MUST conform to RFC 4180 (proper quoting of fields containing commas, quotes, or newlines).
- **FR-008**: When a transaction belongs to multiple categories, the `CategoryIds` CSV cell MUST represent them as a semicolon-delimited list.
- **FR-009**: The export endpoint MUST return all transactions without pagination or date filtering (full dataset only).
- **FR-010**: The response MUST include an appropriate `Content-Disposition: attachment` header with a suggested filename (`transactions.json` or `transactions.csv`) so browsers treat it as a download.

### Key Entities

- **Transaction**: The core domain entity being exported. Each exported record carries `id`, `description`, `amount`, `type` (Income/Expense), `timestamp` (full date-time), and `categoryIds` (one or more integer references).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can retrieve all transactions in JSON format by issuing a single request, with no manual post-processing required.
- **SC-002**: A user can open the CSV export directly in a spreadsheet application without manual correction of any cell value.
- **SC-003**: Every transaction present in the system at request time appears in the export — no records are silently omitted.
- **SC-004**: Requesting an unsupported format returns a 406 response within the same response-time envelope as a successful export.
- **SC-005**: The export endpoint is covered by at least one integration test per supported format and one test for the 406 path.

## Assumptions

- Only the two formats — `application/json` and `text/csv` — are in scope for this feature; no XML, Excel, or other formats.
- The export always returns the full transaction list; date-range or category filtering is out of scope for this feature.
- No authentication or rate limiting is added (consistent with the rest of the API).
- `Timestamp` values in the CSV are formatted as ISO 8601 (`yyyy-MM-ddTHH:mm:ss`) for unambiguous parsing.
- The `Content-Disposition: attachment` filename is static (`transactions.json` / `transactions.csv`), not dynamically generated from a date range.
- The existing `TransactionResponse` DTO (already used by `GET /api/transactions`) is reused as the per-record shape for the JSON export.
- Quality-weighted `Accept` negotiation (e.g., `text/csv;q=0.5, application/json;q=1.0`) is handled by the framework's built-in content-negotiation mechanism; no custom parser is written.
