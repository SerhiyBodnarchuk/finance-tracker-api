# Data Model: MCP Server Integration

All types below are owned by `Finance.Mcp` (a dedicated project). They are distinct from Finance.Data domain entities and Finance.Business DTOs — they never cross the HTTP boundary directly, and they do not appear in Finance.Business or Finance.Api public interfaces.

---

## Enums

### `ContextStatus`

Tracks the lifecycle of an `McpContext`.

| Value | Meaning |
|-------|---------|
| `Active` | Context is stored and accepting operations. |
| `PendingApproval` | A `Confirm` call detected a business-logic-touching change; waiting for `ApproveConfirm`. |
| `Confirmed` | Changes were accepted. Terminal state. |
| `RolledBack` | Changes were discarded via `Rollback`. Terminal state. |
| `Expired` | TTL elapsed before a terminal state was reached. Set lazily on access. |

**Transitions**:
```
Active → PendingApproval  (Confirm, when proposed_changes touch a protected field)
Active → Confirmed        (Confirm, when no protected fields touched)
Active → RolledBack       (Rollback)
Active → Expired          (TTL elapsed, detected on next access)
PendingApproval → Confirmed   (ApproveConfirm)
PendingApproval → RolledBack  (Rollback)
```
Confirmed, RolledBack, and Expired are terminal — no further transitions.

---

### `ActionType`

The type of action the MCP server is requesting the agent to perform.

| Value | Meaning |
|-------|---------|
| `Categorize` | Agent should propose category assignments for the ambiguous transactions. |
| `Validate` | Agent should verify that a prior categorisation is consistent with the current category mappings. |

---

## Records

### `McpContext`

The top-level context stored on the MCP server. One instance per `sendContext` call.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `string` | Non-empty; unique within the store | Assigned by `McpServer.SendContext`. |
| `Version` | `int` | ≥ 1 | Schema version; starts at 1. |
| `Ttl` | `DateTime` (UTC) | > `CreatedAt` | Absolute expiry. Default: 30 minutes after creation; configurable for tests. |
| `CategoryMappings` | `IReadOnlyList<CategoryMappingItem>` | Non-null; may be empty | Snapshot of category id/name/type at context creation time. |
| `PendingTransactions` | `IReadOnlyList<PendingTransactionItem>` | Non-empty | At least one transaction must be in the batch; validated in `SendContext`. |
| `Reconciliation` | `ReconciliationContext` | Non-null | Embedded reconciliation state. |
| `Status` | `ContextStatus` | Valid enum value | Starts as `Active`. |
| `CreatedAt` | `DateTime` (UTC) | Set on construction | Immutable after construction. |

---

### `CategoryMappingItem`

A snapshot entry for one category, embedded in `McpContext.CategoryMappings`.

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `int` | > 0 |
| `Name` | `string` | Non-empty |
| `Type` | `string` | One of: `"Income"`, `"Expense"`, `"Both"` |

---

### `PendingTransactionItem`

A transaction stub embedded in `McpContext.PendingTransactions`. Description is always redacted in serialised output.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `int` | > 0 | |
| `Amount` | `decimal` | > 0 | Unsigned; direction encoded by `TransactionType`. |
| `TransactionType` | `string` | `"Income"` or `"Expense"` | |
| `Date` | `DateTime` | Valid date | Full timestamp, no timezone. |
| `Description` | `string` | Non-null | **Redacted to `"[REDACTED]"` in all serialised snapshots and log entries.** In-memory value is retained for business logic. |

---

### `ReconciliationContext`

Embedded within `McpContext`. Tracks which transactions need categorisation and the current suggestion state.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AmbiguousTransactionIds` | `IReadOnlyList<int>` | Non-empty; must be a subset of `McpContext.PendingTransactions[*].Id` | Set at context creation. |
| `SuggestedCategoryIds` | `IReadOnlyDictionary<int, IReadOnlyList<int>>` | May be empty until `ReceiveResult` is called | Keys are transaction IDs; values are proposed category ID lists. |
| `DecisionStatus` | `ContextStatus` | Mirrors parent `McpContext.Status` | Kept in sync by `McpServer`. |

---

### `AgentActionRequest`

Produced by `McpServer.RequestAction`. Represents the prompt envelope delivered to the agent.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ActionId` | `string` | Non-empty; unique | Assigned by `McpServer`. |
| `ContextId` | `string` | Non-empty | References the parent `McpContext.Id`. |
| `ActionType` | `ActionType` | Valid enum value | |
| `IterationNumber` | `int` | ≥ 1 | Incremented on each re-issue within the verify→refine loop. |
| `PromptText` | `string` | Non-empty | The structured prompt delivered to the agent (includes context summary). |
| `ContextFields` | `IReadOnlyDictionary<string, object>` | Non-null | Minimal redacted context fields needed for the agent to answer (no raw descriptions). |

---

### `AgentResult`

Produced by `McpServer.ReceiveResult`. Represents the agent's response payload.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ActionId` | `string` | Non-empty; must match a pending `AgentActionRequest.ActionId` | |
| `ProposedChanges` | `IReadOnlyList<ProposedChange>` | Non-null; may be empty | Structured diff entries. |
| `Explanation` | `string` | Non-empty | One-line justification from the agent. |
| `ReceivedAt` | `DateTime` (UTC) | Set on construction | |

---

### `ProposedChange`

A single entry in `AgentResult.ProposedChanges`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `TransactionId` | `int` | > 0 | |
| `TargetField` | `string` | Non-empty | e.g., `"categoryIds"`, `"categoryType"`, `"transactionType"` |
| `ProposedValue` | `object` | Non-null | The value to assign. |

A `ProposedChange` where `TargetField` is `"categoryType"` or `"transactionType"` triggers the human approval gate.

---

### `ContextSnapshot`

An immutable, serialised point-in-time copy of an `McpContext`. Used as a replay fixture.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ContextId` | `string` | Non-empty | References the source `McpContext.Id`. |
| `SerialisedAt` | `DateTime` (UTC) | Set on construction | |
| `Hash` | `string` | 64-character hex string (SHA-256) | Hash of `RedactedJson` UTF-8 bytes. |
| `RedactedJson` | `string` | Non-empty; valid JSON | The fully redacted serialised context. All `description` fields replaced with `"[REDACTED]"`. |

---

### `IterationLogEntry`

One record appended to `ai-artifacts/mcp_iteration_log.txt` per `Confirm` or `Rollback` call.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Timestamp` | `string` | ISO-8601 UTC | e.g., `"2026-06-02T14:30:00Z"` |
| `Prompt` | `string` | Non-empty | The `PromptText` from the associated `AgentActionRequest`. |
| `ContextSnapshotHash` | `string` | 64-character hex | SHA-256 of the snapshot taken at the time of the action request. |
| `ModelName` | `string` | Non-empty | e.g., `"claude-sonnet-4-6"` |
| `AgentOutput` | `string` | Non-empty | The raw `Explanation` and `ProposedChanges` from `AgentResult`, serialised. |
| `AcceptedDiff` | `string?` | Nullable | Serialised accepted `ProposedChanges` on confirm; `null` on rollback. |
| `DecisionReason` | `string` | Non-empty | One-line reason: `"confirmed"`, `"rolled back — invalid category"`, etc. |

---

## Validation Rules

| Rule | Enforced by | Error |
|------|-------------|-------|
| `PendingTransactions` must be non-empty | `McpServer.SendContext` | Throws `ArgumentException` |
| `AmbiguousTransactionIds` must be a non-empty subset of `PendingTransactions[*].Id` | `McpServer.SendContext` | Throws `ArgumentException` |
| `RequestAction` on an expired or unknown context ID | `McpServer.RequestAction` | Returns `null` / throws `InvalidOperationException` |
| `ReceiveResult` with an unrecognised `ActionId` | `McpServer.ReceiveResult` | Throws `InvalidOperationException` |
| `Confirm` on an already-confirmed context | `McpServer.Confirm` | Idempotent — returns success without duplicate state change |
| `Rollback` on a confirmed context | `McpServer.Rollback` | Throws `InvalidOperationException` (conflict) |
| `IterationNumber` exceeds the configured maximum | `McpServer.RequestAction` | Triggers automatic rollback; returns a result indicating loop exhaustion |
