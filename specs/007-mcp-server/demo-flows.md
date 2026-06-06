# MCP Server — Demo Flows

Three end-to-end flows that demonstrate the key behaviours of the Finance MCP server.
Run each inside a Claude Code session with the `finance-mcp` server registered and running.

---

## Flow 1 — Happy path: categorise a transaction

**Scenario:** An ambiguous expense transaction is proposed a matching expense category, confirmed automatically, and written into the transaction store.

### Steps

```
Using the Finance MCP tools:

1. Send a context with one transaction: id=10, amount=45.00, type=Expense, description="Tesco".
   Mark it as ambiguous.
2. Request a Categorize action.
3. Receive a result assigning category 2 (Groceries, Expense) to transaction 10,
   explanation "Grocery spend".
4. Confirm the context.
5. Read the finance://transactions resource and verify the new transaction appears.
```

### Expected outcome

| Step | Key values |
|---|---|
| `sendContext` | Returns a context ID |
| `requestAction` | `iterationNumber: 1`, prompt lists transaction 10 |
| `receiveResult` | `OK` |
| `confirm` | `requiresApproval: false`, `status: Confirmed` |
| `finance://transactions` | New entry with `amount: 45.00`, `categoryIds: [2]` visible in the list |

No human approval gate — category type matches transaction type, so the context confirms and the transaction is added to the store immediately.

---

## Flow 2 — Happy path: change transaction type (human approval required)

**Scenario:** The agent decides the transaction was entered as Expense but is actually Income. Because `transactionType` is a business-logic field, the server parks the context for human sign-off. After approval the transaction is added with the corrected type.

### Steps

```
Using the Finance MCP tools:

1. Send a context with one transaction: id=300, amount=120.00, type=Expense,
   description="Freelance payment received". Mark it as ambiguous.
2. Request a Categorize action.
3. Receive a result proposing targetField="transactionType", proposedValue="Income"
   for transaction 300, explanation "This looks like income, not an expense".
4. Call confirm on the context — observe requiresApproval and status.
5. Call approveConfirm on the same context ID.
6. Read finance://transactions and verify the transaction appears with type "Income".
```

### Expected outcome

| Step | Key values |
|---|---|
| `sendContext` | Returns a context ID |
| `requestAction` | `iterationNumber: 1` |
| `receiveResult` | `OK` — business-logic changes are accepted at this stage |
| `confirm` | `requiresApproval: true`, `status: PendingApproval` — transaction NOT yet stored |
| `approveConfirm` | Context moves to `Confirmed`; transaction added to store with `type: Income`; audit log entry written with `decisionReason: "human approved"` |

The auto-confirm path is bypassed whenever a proposed change touches `transactionType` or `categoryType`.

---

## Flow 3 — Blocked: incompatible category type

**Scenario:** The agent mistakenly proposes an Income category for an Expense transaction. The server rejects the result immediately — nothing is stored.

### Steps

```
Using the Finance MCP tools:

1. Send a context with one transaction: id=200, amount=50.00, type=Expense,
   description="Netflix subscription". Mark it as ambiguous.
2. Request a Categorize action.
3. Receive a result assigning category 1 (Salary, Income) to transaction 200.
```

### Expected outcome

| Step | Key values |
|---|---|
| `sendContext` | Returns a context ID |
| `requestAction` | `iterationNumber: 1`, category mappings visible in response |
| `receiveResult` | **Error:** `Category 'Salary' (type=Income) is incompatible with transaction id=200 (type=Expense).` |

The context remains `Active` — the bad proposal is rejected and a corrected `receiveResult` call can follow. No transaction is added to the store.

### Compatibility rules enforced at `receiveResult`

| Transaction type | Allowed category types |
|---|---|
| `Expense` | `Expense`, `Both` |
| `Income` | `Income`, `Both` |

---

## MCP Resources

| URI | Description |
|---|---|
| `finance://transactions` | All transactions currently in the store |
| `finance://transactions/{id}` | Single transaction by ID |
| `finance://categories` | All categories |
| `finance://categories/{id}` | Single category by ID |

Use `finance://transactions` after any confirm step to verify the transaction was applied to the store.

---

## Seed data reference

### Transactions (pre-loaded)

| id | Description | Amount | Type | Categories |
|---|---|---|---|---|
| 1 | Monthly salary | 1200.00 | Income | Salary (1) |
| 2 | Uber Trip | 14.20 | Expense | Transport (3) |
| 3 | Silpo Market | 32.10 | Expense | Groceries (2) |
| 4 | Netflix Subscription | 9.99 | Expense | Entertainment (4) |
| 5 | Electricity Bill | 65.00 | Expense | Utilities (5) |

### Categories

| id | Name | Type |
|---|---|---|
| 1 | Salary | Income |
| 2 | Groceries | Expense |
| 3 | Transport | Expense |
| 4 | Entertainment | Expense |
| 5 | Utilities | Expense |
