# Runtime Profiles

Choose a profile based on the context. All profiles assume `$REPORT_DIR` is set (Step 1) and the schema + base URL are known.

## PR Profile (fast)

Use for pull-request CI checks. Limits workers and phases to keep feedback fast.

```bash
uvx schemathesis run "openapi.yaml" --url "$BASE_URL" \
  --workers 2 \
  --phases examples,fuzzing \
  --report junit --report-junit-path "$REPORT_DIR/junit.xml" \
  --report-har-path "$REPORT_DIR/cassette.har" \
  2>&1 | tee "$REPORT_DIR/schemathesis.log"
```

## Nightly Profile (thorough)

Use for scheduled/nightly runs. Auto-scales workers and runs all phases (the default when `--phases` is omitted).

```bash
uvx schemathesis run "openapi.yaml" --url "$BASE_URL" \
  --workers auto \
  --report junit --report-junit-path "$REPORT_DIR/junit.xml" \
  --report-har-path "$REPORT_DIR/cassette.har" \
  2>&1 | tee "$REPORT_DIR/schemathesis.log"
```

## Available Phases

All phases are enabled by default. Use `--phases` to restrict to a subset.

- `examples` — runs examples specified in the OpenAPI schema
- `coverage` — deterministic testing of schema constraints and boundary values
- `fuzzing` — randomly generated test cases
- `stateful` — chains real API calls, passing response data into subsequent requests
