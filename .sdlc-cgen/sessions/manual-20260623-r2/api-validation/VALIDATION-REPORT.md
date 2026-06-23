# API Contract Validation Report

**Date**: 2026-06-23  
**Schema Source**: `https://localhost:7235/openapi/v1.json` (saved to `.sdlc-cgen/sessions/manual-20260623-r2/spec.json`)  
**Base URL**: `https://localhost:7235`  
**Schemathesis Version**: 4.21.10  
**Profile**: Custom (all phases: Coverage, Fuzzing, Stateful)  
**Overall Result**: FAIL

## Summary

- **Endpoints Tested**: 9/9
- **Total Requests**: 1049 generated
- **Test Phases**: Examples (skipped), Coverage ❌, Fuzzing ❌, Stateful ❌
- **Unique Failures**: 12 (down from 14 in run 1)
  - Critical: 0
  - High: 0 _(timestamp issues resolved)_
  - Medium: 9
  - Low: 3

## Comparison with Run 1 (manual-20260623)

| Issue | Run 1 | Run 2 | Status |
|---|---|---|---|
| Timestamp `date-time` RFC 3339 violation | ❌ High | — | **FIXED** |
| `DELETE` returns 204, only 200 documented | ❌ High | — | **FIXED** |
| `POST /api/Categories` returns 201, only 200 documented | ❌ High | — | **FIXED** |
| `POST /api/Categories` returns 409, undocumented | ❌ High | — | **FIXED** |
| `POST` endpoints: 400 undocumented | ❌ Medium | — | **FIXED** |
| `GET`/`DELETE` endpoints: 404 undocumented | ❌ Medium | — | **FIXED** |
| Error responses use `application/problem+json`, undocumented | new | ❌ Medium | **NEW** |
| Empty 404 body missing Content-Type header | new | ❌ Medium | **NEW** |
| API rejected schema-compliant request (3) | ❌ Low | ❌ Low | carry-over |

---

## Severity Levels

- **Critical** — Server crashes (5xx on valid input), data corruption, security bypass
- **High** — Wrong status codes on success paths, response schema violations that break clients
- **Medium** — Content-type mismatches, missing response headers, undocumented error response shapes
- **Low** — Spec–implementation constraint gaps on edge-case fuzz inputs

---

## Issues

### Medium

#### 1. GET /api/Categories/{id} — 404 body uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: When a valid but non-existent category ID is requested, the API returns a `404` with a `ProblemDetails` JSON body and `Content-Type: application/problem+json`. The spec only documents `text/plain`, `application/json`, `text/json` for the 404 response (inherited from the 200 schema). `application/problem+json` is not listed.
- **Expected**: Documented content types — `text/plain`, `application/json`, `text/json`
- **Actual**: `application/problem+json; charset=utf-8` — `{"title":"Category not found","status":404,"detail":"No category with id 1592."}`
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Categories/1592
```

#### 2. GET /api/Transactions/{id} — 404 body uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: Same as issue #1 for the transactions resource.
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions/1592
```

#### 3. GET /api/Categories/{id} — empty 404 missing Content-Type header

- **Severity**: Medium
- **Description**: When Schemathesis sends an unparseable path value (`null,null`), ASP.NET route binding fails and returns a `404` with an empty body and no `Content-Type` header. The spec documents content types for the 404 response, so Schemathesis flags the missing header.
- **Root cause**: The `["integer","string"]` type union in the spec allows string path values; Schemathesis generates `null,null` as a valid string, which hits a binding failure rather than the controller.
- **Reproducer**:
```bash
curl -X GET --insecure "https://localhost:7235/api/Categories/null%2Cnull"
```

#### 4. GET /api/Transactions/{id} — empty 404 missing Content-Type header

- **Severity**: Medium
- **Description**: Same root cause as issue #3 for the transactions resource.
- **Reproducer**:
```bash
curl -X GET --insecure "https://localhost:7235/api/Transactions/null%2Cnull"
```

#### 5. DELETE /api/Transactions/{id} — empty 404 missing Content-Type header

- **Severity**: Medium
- **Description**: Same root cause as issue #3 for the delete endpoint.
- **Reproducer**:
```bash
curl -X DELETE --insecure "https://localhost:7235/api/Transactions/null%2Cnull"
```

#### 6. POST /api/Categories — 400 response uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: Validation failures on `POST /api/Categories` return `400` with `Content-Type: application/problem+json`. The spec documents only `text/plain`, `application/json`, `text/json` for the 400 response.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/*+json' -d '{"name": "", "type": {}}' --insecure https://localhost:7235/api/Categories
```

#### 7. POST /api/Reports — 400 response uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: Same content-type mismatch as issue #6 for the reports endpoint.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/*+json' -d '{"type": {}, "data": null}' --insecure https://localhost:7235/api/Reports
```

#### 8. POST /api/Transactions — 400 response uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: Same content-type mismatch as issue #6 for the transactions endpoint.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/*+json' \
  -d '{"timestamp":"2000-01-01T00:00:00Z","description":"","amount":"0","transactionType":"Income","categoryIds":[{}]}' \
  --insecure https://localhost:7235/api/Transactions
```

#### 9. Stateful — DELETE then GET returns `application/problem+json` 404, undocumented

- **Severity**: Medium
- **Description**: In stateful scenarios, fetching a just-deleted transaction returns `404` with `application/problem+json` — same content-type issue as #1/#2, surfaced through a stateful chain.
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions
curl -X DELETE --insecure https://localhost:7235/api/Transactions/35583
```

---

### Low

#### 10. POST /api/Categories — empty `name` valid per spec but rejected

- **Severity**: Low
- **Description**: The spec schema places no `minLength` constraint on `name`, so an empty string is schema-valid. The app enforces `Name is required` at the service layer. _(Carry-over from run 1; DataAnnotations on positional record DTOs conflict with ASP.NET model validation — see run-1 report.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"name": "", "type": "Both"}' --insecure https://localhost:7235/api/Categories
```

#### 11. POST /api/Reports — `data: null` valid per spec but rejected

- **Severity**: Low
- **Description**: `ReportRequest.data` is typed as unconstrained `JsonElement`, so `null` is schema-valid. The app requires a typed payload. _(Carry-over from run 1.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"data": null, "type": "Period"}' --insecure https://localhost:7235/api/Reports
```

#### 12. POST /api/Transactions — zero amount/empty fields valid per spec but rejected

- **Severity**: Low
- **Description**: `amount: 0`, empty `description`, and empty `categoryIds` are valid per the spec schema but rejected by app-level validation. _(Carry-over from run 1.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' \
  -d '{"amount":0.0,"categoryIds":[],"description":"","timestamp":"2000-01-01T00:00:00Z","transactionType":"Income"}' \
  --insecure https://localhost:7235/api/Transactions
```

---

## Root Cause of Medium Issues #1–#9

All nine medium issues share a single root cause: when `[ProducesResponseType(StatusCodes.Status4xx)]` is declared without an explicit response-body type, `Microsoft.AspNetCore.OpenApi` inherits the success-response content types (`text/plain`, `application/json`, `text/json`). The actual error responses use `application/problem+json` (ASP.NET's built-in ProblemDetails writer). Fix: declare error responses as `[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]` etc., and add an OpenAPI operation transformer that sets `application/problem+json` as the content type for all `ProblemDetails`-typed responses.

---

## Artifacts

- **JUnit XML**: `.sdlc-cgen/sessions/manual-20260623-r2/api-validation/junit.xml`
- **HAR Cassette**: `.sdlc-cgen/sessions/manual-20260623-r2/api-validation/cassette.har`
- **Execution Log**: `.sdlc-cgen/sessions/manual-20260623-r2/api-validation/schemathesis.log`
- **Schemathesis Seed**: `67298340595584695163685818695921503386`

## Next Steps

1. **Medium #1–#9 (single fix)**: Replace bare `[ProducesResponseType(Status4xx)]` with `[ProducesResponseType<ProblemDetails>(Status4xx)]` on all error-returning actions, then add an OpenAPI operation transformer that emits `application/problem+json` for `ProblemDetails`-typed response schemas.
2. **Low #10–#12**: Accepted as known limitations — fixing requires either class-based DTOs or a custom OpenAPI schema transformer per field.
3. Re-run this skill after step 1 to confirm medium issues resolve.
