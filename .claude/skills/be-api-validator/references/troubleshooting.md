# Troubleshooting

Common Schemathesis errors and how to resolve them.

## Connection Refused

**Symptom**: Schemathesis fails immediately with a connection error.

**Cause**: API server is not running or `BASE_URL` is wrong.

**Fix**:
- Verify the API is running: `curl -sS "$BASE_URL/health"`
- Check the port matches what the server is listening on
- If running in Docker, ensure the container port is mapped to the host

## Schema Loading Failure

**Symptom**: Schemathesis reports it cannot load or parse the schema.

**Cause**: The OpenAPI spec URL/file is unreachable, malformed, or not valid OpenAPI.

**Fix**:
- Verify the schema URL returns valid JSON/YAML: `curl -sS "$SCHEMA_URL" | head -20`
- Validate the spec with an OpenAPI linter before running Schemathesis
- Ensure the file path or URL is correct and accessible

## Authentication Failures (401/403)

**Symptom**: All or most endpoints return 401 Unauthorized or 403 Forbidden.

**Cause**: Auth header is missing, token is expired, or the token lacks required scopes.

**Fix**:
- Confirm the token works: `curl -H "Authorization: Bearer $TOKEN" "$BASE_URL/health"`
- Check token expiration — generate a fresh token before the run
- If using `schemathesis.toml`, verify the env var is exported: `echo $TOKEN`

## Request Timeout

**Symptom**: Tests hang or Schemathesis reports timeout errors.

**Cause**: API is too slow, or `request-timeout` is too low for the endpoints under test.

**Fix**:
- Increase timeout via CLI: `--request-timeout 30`
- Or in `schemathesis.toml`: `request-timeout = 30.0`
- Check if the API has endpoints with expensive operations (reports, aggregations) and consider excluding them from fast profiles

## Non-Zero Exit Code with No Apparent Failures

**Symptom**: Schemathesis exits with a non-zero code but the log shows no test failures.

**Cause**: Internal error, schema loading issue, or all tests were skipped.

**Fix**:
- Check the full log: `cat "$REPORT_DIR/schemathesis.log"`
- Look for error or warning lines near the top of the output
- Try running with a single endpoint to isolate: `--include-path /health`

## Empty JUnit XML

**Symptom**: `junit.xml` exists but contains no test cases.

**Cause**: No endpoints matched the filters, or the schema has no testable operations.

**Fix**:
- Run without filters to confirm endpoints are discovered
- Check that the OpenAPI spec has at least one operation with a response schema defined
