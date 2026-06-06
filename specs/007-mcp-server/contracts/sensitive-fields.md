# Sensitive Fields Policy

`ContextRedactor` enforces this list at serialisation time. Any field whose key (case-insensitive) matches an entry below is replaced with `"[REDACTED]"` in serialised snapshots and log entries. The in-memory value is never modified.

## Prohibited Key Names

| Key (case-insensitive) | Reason |
|------------------------|--------|
| `password` | Credential — must never appear in any context payload |
| `secret` | Catch-all for API keys, client secrets, signing keys |
| `token` | Auth/session tokens |
| `email` | PII — not used in this single-user app but prohibited as a defence-in-depth measure |
| `name` | PII — personal names |
| `accountnumber` | Financial PII |
| `iban` | Financial PII |
| `ssn` | Government identifier |
| `description` | Transaction descriptions may contain merchant names, references, or free-text that could indirectly identify spending patterns; always redacted in external-facing payloads |

## Current Schema Fields That Are Redacted

Only `PendingTransactionItem.Description` is redacted in the current schema version. All other current fields (integer IDs, amounts, dates, category names, and category types) are considered safe to include in snapshots.

## Safety Test Requirement

A unit test (`ContextRedactionSafetyTests`) MUST assert that after serialisation of any `McpContext`, none of the prohibited key names appear anywhere in the resulting JSON string. The test MUST be parameterised over all keys in the prohibited list.

## Adding New Sensitive Fields

When a new field is added to any schema type under `Finance.Business/Mcp/Schema/`:
1. Evaluate whether the field could contain PII, credentials, or sensitive financial data.
2. If yes, add the key name to the prohibited list in `ContextRedactor.ProhibitedKeys`.
3. Add a corresponding entry to this document.
4. Add a parameterised safety test case for the new key.
