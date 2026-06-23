# Schemathesis Configuration

Prefer `schemathesis.toml` over CLI flags for stable, repeatable config. Schemathesis auto-discovers this file in the current or parent directories.

## Basic `schemathesis.toml`

```toml
base-url = "https://${API_HOST}"
request-timeout = 10.0
headers = { Authorization = "Bearer ${TOKEN}" }

[generation]
max-examples = 50
```

Run with env vars:
```bash
export TOKEN="..."
export API_HOST="dev.api.example.com"
uvx schemathesis run openapi.yaml \
  --report junit --report-junit-path "$REPORT_DIR/junit.xml" \
  --report-har-path "$REPORT_DIR/cassette.har" \
  2>&1 | tee "$REPORT_DIR/schemathesis.log"
```

## Environment Variable Substitution

Use `${VAR_NAME}` syntax in values. Schemathesis resolves them at runtime:
```toml
base-url = "https://${ENVIRONMENT}.api.example.com"
headers = { "X-API-Key" = "${API_KEY}" }
```

## Auth Strategies

**Bearer token** (most common):
```toml
headers = { Authorization = "Bearer ${TOKEN}" }
```

**API key header**:
```toml
headers = { "X-API-Key" = "${API_KEY}" }
```

**Multiple headers**:
```toml
headers = { Authorization = "Bearer ${TOKEN}", "X-Tenant-ID" = "${TENANT_ID}" }
```

**Basic auth**:
```toml
[auth]
basic = { username = "${USERNAME}", password = "${PASSWORD}" }
```

**Secret handling**: always use env var interpolation (`${...}`) in the toml file — never hardcode tokens. This ensures secrets stay out of version control and log output.

## Operation-Specific Configuration

Target specific endpoints with `[[operations]]` sections:
```toml
[[operations]]
include-path = "/users"
exclude-method = "POST"

[generation]
max-examples = 200
```

Filtering keys available inside `[[operations]]`: `include-path`, `exclude-method`, `include-name`, `include-tag`, `include-operation-id` (and their `exclude-` counterparts).

## Config Precedence

CLI options take highest precedence and override config file values. Within the config file, more specific settings (operation-level, phase-level) override general settings. See [Schemathesis configuration docs](https://schemathesis.github.io/schemathesis/reference/configuration/) for the full precedence hierarchy.
