# MCP Operations Contract

All operations are methods on `IMcpServer` (in `Finance.Business/Mcp/`). `IMcpClient` exposes the same signatures but accepts and returns only MCP schema types — it has no knowledge of Finance.Business or Finance.Data domain types.

---

## `SendContext`

Stores a new reconciliation context and returns its assigned ID.

```
Input:
  categoryMappings       IReadOnlyList<CategoryMappingItem>   Non-null; snapshot of current categories
  pendingTransactions    IReadOnlyList<PendingTransactionItem> Non-empty
  ambiguousIds           IReadOnlyList<int>                    Non-empty subset of pendingTransactions[*].Id
  ttlMinutes             int?                                  Optional TTL override (default: 30 min)

Output:
  contextId              string                                Unique ID assigned to the new context

Errors:
  ArgumentException      pendingTransactions is empty
  ArgumentException      ambiguousIds is empty or contains IDs not present in pendingTransactions
```

---

## `RequestAction`

Creates an action request for the agent, using the stored context.

```
Input:
  contextId              string        Must reference an Active context
  actionType             ActionType    Categorize | Validate

Output:
  AgentActionRequest                   Contains actionId, promptText, contextFields (redacted), iterationNumber

Errors:
  InvalidOperationException   Context not found, expired, or in a terminal state
  InvalidOperationException   Iteration limit exceeded → context auto-rolled-back; result indicates loop exhaustion
```

---

## `ReceiveResult`

Records the agent's response against a pending action request.

```
Input:
  result                 AgentResult   actionId must match a pending AgentActionRequest

Output:
  void

Errors:
  InvalidOperationException   actionId not found or already resolved
```

---

## `Confirm`

Accepts the agent's proposed changes. Triggers the human approval gate if protected fields are touched.

```
Input:
  contextId              string

Output:
  ConfirmResult
    RequiresApproval     bool      true if context transitioned to PendingApproval
    Status               ContextStatus

Errors:
  InvalidOperationException   Context not found, expired, or already rolled back
  (idempotent if already confirmed — returns success)
```

---

## `Rollback`

Discards the agent's proposed changes and returns the context to a clean terminal state.

```
Input:
  contextId              string
  reason                 string    One-line reason (appended to the iteration log entry)

Output:
  void

Errors:
  InvalidOperationException   Context not found, expired, or already confirmed
```

---

## `ApproveConfirm`

Completes a confirmation that was held in `PendingApproval` by the human approval gate.

```
Input:
  contextId              string

Output:
  void

Errors:
  InvalidOperationException   Context not in PendingApproval state
```

---

## `IMcpClient` Facade

`IMcpClient` mirrors the five primary operations (`SendContext`, `RequestAction`, `ReceiveResult`, `Confirm`, `Rollback`) with identical signatures. It delegates directly to `IMcpServer`. It does not expose `ApproveConfirm` — that is a server-side operation intended for human-in-the-loop approval flows, not automated client pipelines.

The client MUST NOT reference any type from `Finance.Business` or `Finance.Data` other than the types under `Finance.Business/Mcp/Schema/`.
