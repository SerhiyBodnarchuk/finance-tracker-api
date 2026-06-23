# API Contract Validation Report

**Date**: 2026-06-23  
**Schema Source**: `https://localhost:7235/openapi/v1.json` (saved to `.sdlc-cgen/sessions/manual-20260623-r3/spec.json`)  
**Base URL**: `https://localhost:7235`  
**Schemathesis Version**: 4.21.10  
**Profile**: Default (all phases: Coverage, Fuzzing, Stateful)  
**Overall Result**: FAIL

## Summary

- **Endpoints Tested**: 9/9
- **Total Requests**: 1577 generated
- **Test Phases**: Examples (skipped), Coverage ❌, Fuzzing ❌, Stateful ❌
- **Unique Failures**: 12 (unchanged from run 2)
  - Critical: 0
  - High: 0
  - Medium: 9
  - Low: 3

## Comparison with Run 2 (manual-20260623-r2)

| Issue | Run 2 | Run 3 | Status |
|---|---|---|---|
| 404 body uses `application/problem+json`, undocumented (GET by ID) | ❌ Medium | ❌ Medium | carry-over |
| 400 response uses `application/problem+json`, undocumented (POST) | ❌ Medium | ❌ Medium | carry-over |
| Empty 404 body missing Content-Type header (null,null path) | ❌ Medium | ❌ Medium | carry-over |
| Stateful: DELETE then GET returns `application/problem+json` 404 | ❌ Medium | ❌ Medium | carry-over |
| API rejected schema-compliant request (3) | ❌ Low | ❌ Low | carry-over |

### What changed in Run 3 (partially applied fix)

`[ProducesResponseType<ProblemDetails>(StatusCodes.Status4xx)]` was added to all error-returning actions.
This caused the OpenAPI spec to emit the correct **schema** for error responses (`$ref: #/components/schemas/ProblemDetails`)
instead of inheriting the success-response schema. Schema documentation is now correct.

However, the **content type** for those responses still reads `text/plain`, `application/json`, `text/json`
rather than `application/problem+json`. An operation transformer was added to Program.cs to fix this, but the
transformer's runtime type-check (`response.Content.Values.First().Schema is not OpenApiSchemaReference schemaRef`)
does not match the actual runtime type of the schema object at transformer execution time — the transformer silently
no-ops on every response, leaving the content types unchanged. All 9 medium issues carry over.

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
- **Description**: When a valid but non-existent category ID is requested, the API returns a `404` with a `ProblemDetails` JSON body and `Content-Type: application/problem+json`. The spec documents `text/plain`, `application/json`, `text/json` for the 404 response. `application/problem+json` is not listed.
- **Expected**: Documented content types — `text/plain`, `application/json`, `text/json`
- **Actual**: `application/problem+json; charset=utf-8` — `{"title":"Category not found","status":404,"detail":"No category with id 3212."}`
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Categories/3212
```

#### 2. GET /api/Transactions/{id} — 404 body uses `application/problem+json`, undocumented

- **Severity**: Medium
- **Description**: Same as issue #1 for the transactions resource.
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions/3212
```

#### 3. GET /api/Categories/{id} — empty 404 missing Content-Type header

- **Severity**: Medium
- **Description**: When Schemathesis sends `null,null` as the path value, ASP.NET route binding fails and returns a `404` with an empty body and no `Content-Type` header. The spec documents content types for the 404 response, so Schemathesis flags the missing header.
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
curl -X DELETE --insecure https://localhost:7235/api/Transactions/898
```

---

### Low

#### 10. POST /api/Categories — empty `name` valid per spec but rejected

- **Severity**: Low
- **Description**: The spec schema places no `minLength` constraint on `name`, so an empty string is schema-valid. The app enforces `Name is required` at the service layer. _(Carry-over.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"name": "", "type": "Both"}' --insecure https://localhost:7235/api/Categories
```

#### 11. POST /api/Reports — `data: null` valid per spec but rejected

- **Severity**: Low
- **Description**: `ReportRequest.data` is typed as unconstrained `JsonElement`, so `null` is schema-valid. The app requires a typed payload. _(Carry-over.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"data": null, "type": "Period"}' --insecure https://localhost:7235/api/Reports
```

#### 12. POST /api/Transactions — zero amount/empty fields valid per spec but rejected

- **Severity**: Low
- **Description**: `amount: 0`, empty `description`, and empty `categoryIds` are valid per the spec schema but rejected by app-level validation. _(Carry-over.)_
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' \
  -d '{"amount":0.0,"categoryIds":[],"description":"","timestamp":"2000-01-01T00:00:00Z","transactionType":"Income"}' \
  --insecure https://localhost:7235/api/Transactions
```

---

## Root Cause of Medium Issues #1–#9 (unchanged)

All nine medium issues share one root cause: error responses use `application/problem+json` (ASP.NET's ProblemDetails writer, RFC 7807), but the OpenAPI spec documents them with the inherited success-response content types (`text/plain`, `application/json`, `text/json`).

### What run 3 fixed (schema layer — correct)
`[ProducesResponseType<ProblemDetails>(StatusCodes.Status4xx)]` now emits the correct ProblemDetails *schema* (`$ref: #/components/schemas/ProblemDetails`) for error responses. The schema documentation is now accurate.

### What run 3 did NOT fix (content-type layer — transformer bug)
The operation transformer added to `Program.cs` was intended to replace the content-type keys with `application/problem+json`, but its condition uses a runtime type-check (`Schema is not OpenApiSchemaReference schemaRef`) that does not match the actual type of the schema object at transformer execution time (OpenAPI.NET v2 / .NET 10). The transformer is never applied.

**Fix needed**: Replace the runtime `is not OpenApiSchemaReference` type-check with a status-code-based condition. Since all 4xx/5xx responses in this API use `ProblemDetails`, the transformer should target any response with HTTP status >= 400:

```csharp
options.AddOperationTransformer((operation, _, _) =>
{
    foreach (var (statusCode, response) in operation.Responses)
    {
        if (response.Content is not { Count: > 0 }) continue;
        if (!int.TryParse(statusCode, out var code) || code < 400) continue;

        foreach (var key in response.Content.Keys.ToList())
            response.Content.Remove(key);

        response.Content["application/problem+json"] =
            new OpenApiMediaType { Schema = new OpenApiSchemaReference("ProblemDetails") };
    }
    return Task.CompletedTask;
});
```

---

## Artifacts

- **JUnit XML**: `.sdlc-cgen/sessions/manual-20260623-r3/api-validation/junit.xml`
- **HAR Cassette**: `.sdlc-cgen/sessions/manual-20260623-r3/api-validation/cassette.har`
- **Execution Log**: `.sdlc-cgen/sessions/manual-20260623-r3/api-validation/schemathesis.log`
- **Schemathesis Seed**: `480861425225917472229855865920144756`

## Next Steps

1. **Medium #1–#9**: Apply the status-code-based transformer fix shown above to `Program.cs`, rebuild and restart the API, then re-run this skill for run 4.
2. **Low #10–#12**: Accepted as known limitations — fixing requires either class-based DTOs with DataAnnotations or a custom OpenAPI schema transformer per constrained field.
