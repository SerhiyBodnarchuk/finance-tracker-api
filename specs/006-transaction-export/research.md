# Research: Transaction Export (JSON / CSV)

## Content Negotiation Strategy

**Decision**: Manual `Accept` header inspection in the controller action using ASP.NET Core's `Request.GetTypedHeaders().Accept` (returns a quality-sorted list of `MediaTypeHeaderValue` objects).

**Rationale**: A full custom `IOutputFormatter` pipeline is the "ASP.NET Core way" but introduces non-trivial complexity (formatter registration, `ObjectResult` wrapping, serialiser configuration) for two supported media types in a single-user pet project. Manual inspection using the framework's typed-header parser gives correct `q=`-value ordering without writing a custom parser, and keeps the logic readable and trivially testable as a controller unit test.

**Alternatives considered**:
- Custom `IOutputFormatter` — correct in framework terms but disproportionately complex; formatter lifecycle and `ProducesAttribute` interaction add hidden coupling.
- Checking `Accept` as a raw string — quick but fails on quality-weighted headers (e.g., `text/csv;q=0.5, application/json;q=1.0`); replaced by the typed-header approach.

---

## CSV Format Generation Location

**Decision**: A new static class `TransactionCsvFormatter` in `Finance.Business/Export/`. It takes `IEnumerable<TransactionResponse>` and returns a `string`.

**Rationale**: The constitution's Principle IV explicitly states "Export logic for JSON and CSV — split across Finance.Business.UnitTests (format generation) and Finance.Api.IntegrationTests (content negotiation)". Placing the formatter in `Finance.Business` makes it independently unit-testable there. It has no ASP.NET Core dependency (pure string manipulation), so the layer boundary is preserved.

**Alternatives considered**:
- Inline CSV building in the controller — violates the constitution's test coverage split; makes the controller harder to unit-test.
- Injectable `ICsvFormatter<T>` service — useful if multiple export types were planned; over-engineered for two types in MVP.

---

## RFC 4180 CSV Compliance

**Decision**: Implement RFC 4180 quoting in `TransactionCsvFormatter`:
1. Always emit a header row: `Id,Description,Amount,Type,Timestamp,CategoryIds`.
2. Quote a field by wrapping it in `"..."` and escaping internal `"` as `""` when the field contains a comma, double-quote, or newline.
3. Represent multi-category `CategoryIds` as a semicolon-delimited list within the cell (e.g., `"1;2;3"`). Since semicolons cannot be confused with the field delimiter, quoting is only needed if the cell also contains commas or quotes — which it never will for integer IDs. However, the formatter will apply the standard quoting rule uniformly so the logic is generic.
4. `Timestamp` formatted as ISO 8601 `yyyy-MM-ddTHH:mm:ss` (no timezone suffix — `DateTime` has no zone, per Principle III).
5. `Amount` formatted with full decimal precision using invariant culture (no locale-specific decimal separator).

**Alternatives considered**:
- Using a third-party CSV library (CsvHelper, etc.) — out of scope; adds an external dependency for straightforward logic.
- Tab-delimited — not CSV; breaks spreadsheet import assumptions.

---

## Endpoint Path

**Decision**: `GET /api/transactions/export` as an additional action on the existing `TransactionsController`.

**Rationale**: Groups naturally with other transaction endpoints; no new controller needed; `[HttpGet("export")]` in the existing class is the minimal change.

**Route conflict check**: The existing `[HttpGet("{id:int}")]` uses an `int` constraint, so a string literal `"export"` cannot be matched by that route. No conflict.

---

## Content-Disposition Header

**Decision**: `Content-Disposition: attachment; filename=transactions.json` (or `.csv`) set directly on `Response.Headers` before returning the result.

**Rationale**: Ensures browsers offer a Save dialog rather than inline display. Static filenames are sufficient per spec assumption; dynamic date-range filenames are out of scope.

---

## Default Format (Accept: */* or absent)

**Decision**: Default to `application/json` when `Accept` is absent or `*/*`.

**Rationale**: JSON is the API's native format and the lowest-surprise default for API clients. The spec states this explicitly (FR-005).

---

## 406 Behaviour

**Decision**: Return `StatusCode(406)` with an empty body when no supported media type is found in the `Accept` header.

**Rationale**: HTTP 406 Not Acceptable is the correct status; no `ProblemDetails` body is required (content-type negotiation itself failed, so emitting JSON problem details would be ironic — the client has declared it cannot accept it). A bare `406` with empty body is the safest choice.
