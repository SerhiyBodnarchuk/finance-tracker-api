# API Contract Validation Report

**Date**: 2026-06-23  
**Schema Source**: `https://localhost:7235/openapi/v1.json` (saved to `.sdlc-cgen/sessions/manual-20260623/spec.json`)  
**Base URL**: `https://localhost:7235`  
**Schemathesis Version**: 4.21.10  
**Profile**: Custom (all phases: Coverage, Fuzzing, Stateful)  
**Overall Result**: FAIL

## Summary

- **Endpoints Tested**: 9/9
- **Total Requests**: 790 generated
- **Test Phases**: Examples (skipped — no examples in spec), Coverage ❌, Fuzzing ❌, Stateful ❌
- **Unique Failures**: 14
  - Critical: 0
  - High: 5
  - Medium: 6
  - Low: 3

---

## Severity Levels

- **Critical** — Server crashes (5xx on valid input), data corruption, security bypass
- **High** — Wrong status codes on success paths, response body schema violations that break clients
- **Medium** — Missing error status codes in spec, spec–implementation constraint gaps
- **Low** — Edge-case fuzz inputs; type-union schema confusion in path parameters

---

## Issues

### High

#### 1. GET /api/Transactions — Response timestamp violates `date-time` format

- **Severity**: High
- **Description**: The `TransactionResponse.timestamp` field is declared as `format: date-time` (RFC 3339), but the API serialises `DateTime` without a timezone offset. Clients validating the response against the spec will reject every transaction response.
- **Expected**: `"2026-05-01T12:00:00Z"` or `"2026-05-01T12:00:00+00:00"` (RFC 3339 `date-time`)
- **Actual**: `"2026-05-01T12:00:00"` (no offset — not a valid `date-time`)
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions
```

#### 2. GET /api/Transactions/{id} — Response timestamp violates `date-time` format

- **Severity**: High
- **Description**: Same timestamp format violation as issue #1 on the single-resource endpoint.
- **Expected**: Timezone-offset timestamp per RFC 3339
- **Actual**: `"2026-05-01T12:00:00"` (no offset)
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions/1
```

#### 3. DELETE /api/Transactions/{id} — 204 on success, only 200 documented

- **Severity**: High
- **Description**: A successful delete returns `204 No Content`, which is the correct REST convention, but the spec only documents `200`. Any client performing strict status-code validation will treat `204` as an error.
- **Expected**: `200 OK` (per spec)
- **Actual**: `204 No Content`
- **Reproducer**:
```bash
curl -X DELETE --insecure https://localhost:7235/api/Transactions/2
```

#### 4. POST /api/Categories — 201 on success, only 200 documented

- **Severity**: High
- **Description**: Creating a category returns `201 Created`, but the spec only documents `200`. Same strict-client breakage concern as issue #3.
- **Expected**: `200 OK` (per spec)
- **Actual**: `201 Created` — e.g. `{"id":6,"name":"f-]w÷","type":"Expense"}`
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' \
  -d '{"name": "TestCat", "type": "Expense"}' \
  --insecure https://localhost:7235/api/Categories
```

#### 5. POST /api/Categories — 409 Conflict not documented (stateful)

- **Severity**: High
- **Description**: In stateful flows, re-creating a category with a duplicate name returns `409 Conflict` with a human-readable problem-details body. `409` is not declared in the spec responses for `POST /api/Categories`.
- **Expected**: `200` or `201` (per spec)
- **Actual**: `409 Conflict` — `{"title":"Duplicate category name","status":409,"detail":"A category named 'Utilities' already exists."}`
- **Reproducer**:
```bash
# Create the same name twice
curl -X POST -H 'Content-Type: application/json' -d '{"name": "Utilities", "type": "Both"}' --insecure https://localhost:7235/api/Categories
curl -X POST -H 'Content-Type: application/json' -d '{"name": "Utilities", "type": "Both"}' --insecure https://localhost:7235/api/Categories
```

---

### Medium

#### 6. GET /api/Categories/{id} — 404 not documented

- **Severity**: Medium
- **Description**: Requesting a non-existent category ID returns `404 Not Found` (empty body), but `404` is not listed in the spec responses.
- **Expected**: Only `200` is documented
- **Actual**: `404 Not Found` (empty body)
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Categories/9999
```

#### 7. GET /api/Transactions/{id} — 404 not documented

- **Severity**: Medium
- **Description**: Same as issue #6 for the transactions resource.
- **Reproducer**:
```bash
curl -X GET --insecure https://localhost:7235/api/Transactions/9999
```

#### 8. DELETE /api/Transactions/{id} — 404 not documented

- **Severity**: Medium
- **Description**: Deleting a non-existent transaction ID returns `404 Not Found`, undocumented.
- **Reproducer**:
```bash
curl -X DELETE --insecure https://localhost:7235/api/Transactions/9999
```

#### 9. POST /api/Categories — 400 validation errors not documented

- **Severity**: Medium
- **Description**: Invalid request bodies (empty `name`, wrong `type` shape) return `400 Bad Request` with a problem-details body. `400` is not declared in the spec.
- **Actual**: `{"type":"...rfc9110...","title":"One or more validation errors occurred.","status":400,"errors":{"$.type":["The JSON value could not be converted..."]}}`
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"name": "", "type": {}}' --insecure https://localhost:7235/api/Categories
```

#### 10. POST /api/Reports — 400 not documented

- **Severity**: Medium
- **Description**: Invalid or missing `data` payload returns `400`, undocumented in spec.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"type": "Period", "data": null}' --insecure https://localhost:7235/api/Reports
```

#### 11. POST /api/Transactions — 400 not documented

- **Severity**: Medium
- **Description**: Validation failures (zero amount, empty description, empty `categoryIds`) return `400`, undocumented.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' \
  -d '{"amount": 0.0, "categoryIds": [], "description": "", "timestamp": "2000-01-01T00:00:00Z", "transactionType": "Income"}' \
  --insecure https://localhost:7235/api/Transactions
```

---

### Low

#### 12. POST /api/Categories — App rejects empty `name` not restricted by spec schema

- **Severity**: Low
- **Description**: The spec schema for `CategoryCreateRequest.name` declares only `type: string` with no `minLength`, so an empty string is spec-valid. The app enforces `Name is required` at the service layer. Schemathesis classifies this as "API rejected schema-compliant request". Fix: add `minLength: 1` to the spec schema.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"name": "", "type": "Both"}' --insecure https://localhost:7235/api/Categories
```

#### 13. POST /api/Reports — `data: null` valid per `JsonElement` schema but rejected

- **Severity**: Low
- **Description**: `ReportRequest.data` is typed as `JsonElement` in the spec (unconstrained), so `null` is schema-valid. The app requires a typed payload. Fix: narrow the spec to express that `data` must be a non-null object.
- **Reproducer**:
```bash
curl -X POST -H 'Content-Type: application/json' -d '{"data": null, "type": "Period"}' --insecure https://localhost:7235/api/Reports
```

#### 14. Path parameters typed as `["integer", "string"]` confuse Schemathesis

- **Severity**: Low
- **Description**: ASP.NET OpenAPI emits `"type": ["integer", "string"]` for `int32` path parameters (a JSON Schema 2020-12 union). Schemathesis treats the `string` branch as a valid type and generates values like `null,null` which don't parse as integers, causing 404s from failed route binding. The 404 responses are correct; the root issue is the generated spec's type union. Fix: emit `"type": "integer"` only for integer path parameters, or add a `pattern` constraint that Schemathesis respects.
- **Affected**: `GET /api/Categories/{id}`, `GET /api/Transactions/{id}`, `DELETE /api/Transactions/{id}`

---

## Artifacts

- **JUnit XML**: `.sdlc-cgen/sessions/manual-20260623/api-validation/junit.xml`
- **HAR Cassette**: `.sdlc-cgen/sessions/manual-20260623/api-validation/cassette.har`
- **Execution Log**: `.sdlc-cgen/sessions/manual-20260623/api-validation/schemathesis.log`
- **Command Used**:
  ```powershell
  $env:PYTHONUTF8 = "1"
  schemathesis run spec.json --url https://localhost:7235 --tls-verify=false `
    --report junit --report-junit-path junit.xml `
    --report har --report-har-path cassette.har
  ```
- **Schemathesis Seed**: `311056812805463519525690168327781861411`

---

## Next Steps

1. **High — Timestamp format (#1, #2)**: Configure ASP.NET JSON serialiser to append `Z` or a UTC offset to `DateTime` values. Set `JsonSerializerOptions.DefaultIgnoreCondition` or use `DateTimeKind.Utc` throughout seeded data and service layer.
2. **High — Status code mismatches (#3, #4, #5)**: Either update the spec to document `201`, `204`, and `409` responses, or align the controller to return `200` on success. The `201`/`204` responses are semantically correct; the easier fix is updating the spec.
3. **Medium — Missing error responses (#6–#11)**: Add `400` and `404` response entries to all operations in the OpenAPI spec.
4. **Low — Schema constraints (#12, #13)**: Add `minLength: 1` to `CategoryCreateRequest.name`; narrow `ReportRequest.data` type.
5. **Low — Path parameter type union (#14)**: Investigate whether net10.0 `Microsoft.AspNetCore.OpenApi` can be configured to emit `"type": "integer"` only, or add an `[OpenApiSchema]` attribute override.
6. Re-run this skill after fixes to confirm all 14 failures resolve.
