# MCP Server — Conversational Test Prompts

These prompts are written for use **inside a Claude Code session** once the Finance MCP server
is registered as a tool (see prerequisites below). Each prompt is a plain-English instruction —
paste it as your next message and Claude Code will call the MCP tools directly.

---

## Prerequisites

Before using these prompts the Finance MCP server must be running and registered:

1. Build the MCP host: `dotnet build src/backend/FinanceTracker/Finance.Mcp.Host`
2. Register it in `.claude/settings.json` under `mcpServers`:
   ```json
   "finance-mcp": {
     "command": "dotnet",
     "args": ["run", "--project", "src/backend/FinanceTracker/Finance.Mcp.Host", "--no-build"]
   }
   ```
3. Restart Claude Code — the tools `sendContext`, `requestAction`, `receiveResult`,
   `confirm`, and `rollback` should appear in `/tools`.
4. The server now resolves category mappings from `ICategoryService` automatically —
   **do not pass `categoryMappings` to `sendContext`**. To verify available IDs before
   a run, read the `finance://categories` MCP resource. Current seed (for reference):
   - id=1  Salary        Income
   - id=2  Groceries     Expense
   - id=3  Transport     Expense
   - id=4  Entertainment Expense
   - id=5  Utilities     Expense

---

## 1. Full round-trip (happy path)

```
Using the Finance MCP server tools, run a full reconciliation round-trip:

1. Send a context with:
   - Pending transactions: one transaction id=10, amount=45.00, type=Expense, description="Tesco"
   - Ambiguous IDs: [10]
2. Request a categorization action on the context you just created.
3. Receive a result that assigns transaction 10 to category 2 (Groceries) with explanation "Grocery spend".
4. Confirm the context.

After each step tell me the key field values returned (context ID, action ID, prompt text,
confirm status). At the end confirm the status is "Confirmed" and RequiresApproval is false.
```

---

## 2. Rollback flow

```
Using the Finance MCP tools:

1. Send a context with one transaction (id=20, amount=120.00, type=Income, description="Freelance").
   Mark it as ambiguous.
2. Request a categorization action.
3. Receive a result proposing category 1 (Salary) for transaction 20.
4. Roll back the context with reason "wrong category proposed".

Confirm the rollback succeeded and then try to confirm the same context ID.
Tell me whether an error is returned and what it says.
```

---

## 3. Verify→refine loop — second attempt succeeds

```
Using the Finance MCP tools, simulate a two-iteration verify→refine loop:

1. Send a context with two transactions: id=50 (amount=30, type=Expense) and id=51 (amount=80, type=Expense).
   Both are ambiguous.
2. Request a categorization action (iteration 1). Show me the PromptText and IterationNumber.
3. Receive a result for iteration 1 that has an empty ProposedChanges list — treat this as a
   failed validation that requires a retry.
4. Request a second categorization action (iteration 2). Show me the new IterationNumber.
5. Receive a result for iteration 2 that assigns category 2 (Groceries) to both transactions.
6. Confirm the context.

Tell me the iteration numbers from each RequestAction call and confirm the final status.
```

---

## 4. Loop exhaustion auto-rollback

```
Using the Finance MCP tools (the server is configured with maxIterations=3):

1. Send a context with transaction id=60, amount=200, type=Expense, ambiguous.
2. Call requestAction three times in a row without ever calling receiveResult.
3. On the third call tell me what PromptText is returned — it should say "[LOOP EXHAUSTED]".
4. Then try to call requestAction a fourth time and tell me whether an error is returned.

Confirm whether the context was automatically rolled back after the loop was exhausted.
```

---

## 5. Business-logic change requires human approval

```
Using the Finance MCP tools:

1. Send a context with transaction id=70, amount=50, type=Expense, ambiguous.
2. Request a categorization action.
3. Receive a result where the ProposedChange targets the field "categoryType" with value "Income"
   — this touches a business-logic rule.
4. Call confirm on the context.

Tell me whether RequiresApproval is true and the status is "PendingApproval".
Then call approveConfirm on the same context ID and tell me the resulting status.
```

---

## 6. Privacy check — no sensitive fields in snapshot

```
Using the Finance MCP tools:

1. Send a context with a transaction whose description is "IBAN GB12BARC secret passphrase".
2. Take a snapshot of that context.
3. Show me the full RedactedJson from the snapshot.

Verify that:
- The string "IBAN GB12BARC secret passphrase" does NOT appear in the JSON.
- The string "[REDACTED]" DOES appear in place of the description.
- None of these property names appear anywhere in the JSON:
  "password", "secret", "token", "email", "name", "accountNumber", "iban", "ssn", "description".
```

---

## 7. Replay determinism

```
Using the Finance MCP tools, replay the same context three times and compare outputs:

Fixed input for each run:
- Pending transactions: id=1, amount=10.00, type=Expense, date=2026-01-15T09:00:00, description="Fixed"
- Ambiguous IDs: [1]

Fixed agent result for each run:
- ProposedChanges: [{ transactionId=1, targetField="categoryId", proposedValue=2 }]
- Explanation: "deterministic"

For each run: sendContext → requestAction → receiveResult → takeSnapshot.
Use a brand-new server session each time (or call rollback between runs so state is clean).

After three runs show me the three snapshot hashes and confirm whether they are identical.
```

---

## 8. Expired context is rejected

```
Using the Finance MCP tools:

1. Send a context with ttlMinutes set to -1 (expired immediately).
2. Immediately call requestAction on the returned context ID.

Tell me whether an error is returned and what it says. It should indicate the context
was not found or has expired.
```

---

## 9. Unknown action ID is rejected

```
Using the Finance MCP tools:

1. Send a valid context with one transaction (id=80, ambiguous).
2. Call receiveResult with actionId="nonexistent-action-id-12345" and any proposed changes.

Tell me whether an error is returned and what the error message says.
```

---

## 10. Empty pending-transactions rejected at sendContext

```
Using the Finance MCP tools, try to send a context with an empty pendingTransactions list.
Tell me whether the server rejects it and what the error says.
Then try a second call where pendingTransactions has one item but ambiguousIds is empty.
Tell me whether that is also rejected and why.
```

---

## Notes

- Prompts 4 and 7 assume the server instance is either freshly started or that state from
  previous calls has been cleared. If the server is stateful across prompts, start a new
  session or call `rollback` on leftover context IDs before running them.
- The iteration log at `ai-artifacts/mcp_iteration_log.txt` should gain a new entry after
  every `confirm` or `rollback` call — after any of the above prompts you can ask:
  *"Show me the last entry in ai-artifacts/mcp_iteration_log.txt"* to verify FR-010.
