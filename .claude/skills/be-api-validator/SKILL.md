---
name: be-api-validator
description: Run Schemathesis contract tests against a running API from an OpenAPI spec. Produces CI artifacts (JUnit XML, HAR, logs) and a structured validation report.
---

# API Contract Testing

Run **Schemathesis** against an API implementation from the session **OpenAPI spec** and produce **repeatable artifacts** for CI and reporting.

## Invocation Inputs

This skill is invoked by the testing node with the following inputs:

```
SESSION_ID   — workflow session identifier
SPEC_PATH    — absolute path to .sdlc-cgen/sessions/<session_id>/spec.yaml
OUTPUT_DIR   — absolute path to .sdlc-cgen/sessions/<session_id>/api-validation/
```

## When to use

- You have an OpenAPI 3.x spec and want automated contract coverage
- You want a CI gate validating implementation vs spec
- You want reproducible bug reports with reproducers and artifacts

## When NOT to use

- Unit or integration test execution (use the project's test runner)
- Static code analysis or linting
- Manual QA or exploratory testing
- Fixing issues (this skill only reports them)

## Preconditions

- The API under test is **running and reachable**
- `SPEC_PATH` exists (`.sdlc-cgen/sessions/<session_id>/spec.yaml`, written by the planning agent)
- `BASE_URL` is discoverable from `package.json` scripts, `README.md`, `.env.example`, or `docker-compose.yml`
- Auth details available via `.env.test` / `.env.test.local` if protected (token in `$TOKEN`)

---

## Step 0 — Verify spec and check API reachability

Check that `SPEC_PATH` exists:

```bash
test -f "$SPEC_PATH" || { echo "spec.yaml missing at $SPEC_PATH"; exit 1; }
```

Discover `BASE_URL` from project config (do NOT prompt the user). Optionally verify:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" "$BASE_URL/health" || true
```

---

## Step 1 — Create output directory

```bash
mkdir -p "$OUTPUT_DIR"
```

Artifacts will be written to `$OUTPUT_DIR`:
- `junit.xml`
- `cassette.har`
- `schemathesis.log`
- `VALIDATION-REPORT.md`

---

## Step 2 — Choose execution mode

### A) Preferred: `uvx` (no install)
```bash
uvx schemathesis --version
```

### B) Virtualenv install (fallback)
```bash
python -m venv .venv
. .venv/bin/activate
python -m pip install -U pip
python -m pip install schemathesis
schemathesis --version
```

---

## Step 3 — Run Schemathesis

Schema is the local file at `SPEC_PATH`:

```bash
uvx schemathesis run "$SPEC_PATH" --url "$BASE_URL" \
  --report junit --report-junit-path "$OUTPUT_DIR/junit.xml" \
  --report-har-path "$OUTPUT_DIR/cassette.har" \
  2>&1 | tee "$OUTPUT_DIR/schemathesis.log"
```

### Auth header (if protected)

```bash
uvx schemathesis run "$SPEC_PATH" --url "$BASE_URL" \
  --header "Authorization: Bearer $TOKEN" \
  --report junit --report-junit-path "$OUTPUT_DIR/junit.xml" \
  --report-har-path "$OUTPUT_DIR/cassette.har" \
  2>&1 | tee "$OUTPUT_DIR/schemathesis.log"
```

**Secret handling**: always store tokens in env vars (`$TOKEN`), never hardcode. Load from `.env.test` / `.env.test.local`. Schemathesis has output sanitization enabled by default. For stable auth config, prefer `schemathesis.toml` with `${VAR}` interpolation — see [configuration.md](references/configuration.md).

---

## Step 4 — Runtime profiles

For **PR (fast)** and **nightly (thorough)** profiles and available phases, see [runtime-profiles.md](references/runtime-profiles.md).

---

## Step 5 — Stable config via `schemathesis.toml`

For repeatable runs, prefer a `schemathesis.toml` config file over CLI flags. See [configuration.md](references/configuration.md) for the full format, env var substitution, auth strategies, and operation-specific overrides.

---

## Step 6 — Verify artifacts exist (required)

```bash
test -f "$OUTPUT_DIR/junit.xml"
test -f "$OUTPUT_DIR/cassette.har"
test -f "$OUTPUT_DIR/schemathesis.log"
```

---

## Step 7 — Generate validation report (required)

Generate a report using the [report template](assets/API-CONTRACT-VALIDATION-REPORT-TEMPLATE.md). For each failure, capture:

- Method + path + operationId (if present)
- Expected vs actual (status code, response schema, content-type/headers)
- Reproducer (include the reproduction snippet printed by Schemathesis)
- Attach all artifacts from `$OUTPUT_DIR`

Save the completed report to `$OUTPUT_DIR/VALIDATION-REPORT.md`.

---

## Step 8 — Call job_done

Read `$OUTPUT_DIR/VALIDATION-REPORT.md` and `$OUTPUT_DIR/junit.xml`. For each failure, build a structured issue and classify its severity:
- **critical** — 5xx server errors, security violations
- **major** — conformance failures, schema mismatches, unexpected 4xx
- **minor** — missing optional fields, deprecation warnings

Cap issues at 50. Build a parallel flat `failures[]` as `"<METHOD> <path> [<status>] — <title>"`.

Call `job_done` with:

```json
{
  "passed": "<true if zero issues>",
  "total": "<number of test cases run>",
  "failures": ["<METHOD> <path> [<status>] — <title>", "..."],
  "issues": [
    {
      "method": "POST",
      "path": "/users",
      "severity": "critical",
      "title": "<concise assertion failure description>",
      "details": "<full Schemathesis reproducer or assertion text (optional)>"
    }
  ],
  "coverage_percent": 0,
  "tester": "schemathesis",
  "summary": "<one paragraph summary of results>",
  "report_path": "<OUTPUT_DIR>"
}
```

---

## Troubleshooting

If Schemathesis fails to connect, parse the schema, or returns unexpected errors, see [troubleshooting.md](references/troubleshooting.md).

## References

- [runtime-profiles.md](references/runtime-profiles.md) — PR, nightly profiles and phase descriptions
- [configuration.md](references/configuration.md) — `schemathesis.toml` format, env vars, auth, operation overrides
- [troubleshooting.md](references/troubleshooting.md) — Common errors and fixes
- [API-CONTRACT-VALIDATION-REPORT-TEMPLATE.md](assets/API-CONTRACT-VALIDATION-REPORT-TEMPLATE.md) — Report template for Step 7
