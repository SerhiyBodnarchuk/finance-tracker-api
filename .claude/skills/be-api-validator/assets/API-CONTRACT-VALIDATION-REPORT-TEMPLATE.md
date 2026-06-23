# API Contract Validation Report

**Date**: YYYY-MM-DD HH:MM:SS
**Schema Source**: [URL or file path to OpenAPI spec]
**Base URL**: [API base URL tested against]
**Schemathesis Version**: [version]
**Profile**: [PR-fast / Nightly-thorough / Custom]
**Overall Result**: [PASS / FAIL]

## Summary

- **Endpoints Tested**: X/Y (covered/total from spec)
- **Total Requests**: X
- **Failures**: X
  - Critical: X
  - High: X
  - Medium: X
  - Low: X

## Severity Levels

- **Critical** — Server crashes (5xx on valid input), data corruption, security bypass (e.g., `ignored_auth` check failure)
- **High** — Wrong status codes, missing or extra fields in response body, contract violations that break clients
- **Medium** — Content-type mismatch, missing response headers, schema conformance issues on edge-case inputs
- **Low** — Timeout on slow endpoints, deprecated endpoint warnings, minor spec deviations on negative/fuzz inputs

## Issues

<!-- Repeat this block for each failure. Remove section if no failures. -->

### Critical

#### 1. [METHOD /path (operationId)]

- **Severity**: Critical
- **Description**: Brief description of the failure
- **Expected**: What the OpenAPI spec declares (status code, response schema, content-type)
- **Actual**: What the API returned
- **Reproducer**:
```bash
curl -X METHOD "BASE_URL/path" -H "..." -d '...'
```
<!-- Or paste the schemathesis reproducer snippet from the log -->

### High

#### 2. [METHOD /path (operationId)]

- **Severity**: High
- **Description**: Brief description of the failure
- **Expected**: What the OpenAPI spec declares
- **Actual**: What the API returned
- **Reproducer**:
```bash
curl -X METHOD "BASE_URL/path" -H "..." -d '...'
```

### Medium

<!-- Add medium-severity issues here -->

### Low

<!-- Add low-severity issues here -->

## Artifacts

- **JUnit XML**: `$REPORT_DIR/junit.xml`
- **HAR Cassette**: `$REPORT_DIR/cassette.har`
- **Execution Log**: `$REPORT_DIR/schemathesis.log`
- **Command Used**: [full schemathesis command]
- **Base URL**: [value]
- **Schemathesis Version**: [value]

## Next Steps

### If FAIL
1. Fix Critical issues immediately (server errors, security bypasses)
2. Fix High issues next (contract violations that break clients)
3. Address Medium issues (conformance edge cases)
4. Triage Low issues (may be acceptable or deferred)
5. Re-run this skill after fixes to verify resolution

### If PASS
- API contract compliance confirmed for tested endpoints
- Consider adding this run to the CI pipeline as a gate
- Schedule a nightly thorough profile run for deeper coverage
