# AI Agent Specification: Add Seeded In-Memory Repositories

You are working in a .NET API-only repository named `finance-tracker-api`.

Implement seeded in-memory storage for a single-user personal finance API.

## Project Constraints

- API-only project.
- No frontend.
- No authentication.
- No authorization.
- No multi-user support.
- No SQL Server.
- No EF Core.
- No Docker.
- No LocalDB.
- No database migrations.
- Data may reset every time the application restarts.
- Use seeded in-memory repositories.
- Keep implementation simple, testable, and suitable for a one-week assignment MVP.

## Goal

Create the repository layer for transactions and categories using in-memory storage. The data should be seeded on application startup so the API has useful demo data immediately.

## Domain Model Requirements

Create or update domain models as needed.

### Transaction

A transaction should have:

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Unique transaction identifier |
| `Date` | `DateOnly` | Transaction date |
| `Description` | `string` | Required transaction description |
| `Amount` | `decimal` | Positive amount from the API request |
| `TransactionType` | enum or string | Values: `Income`, `Expense` |
| `CategoryId` | `Guid` | Required category reference |

Rules:

- Amount should be positive in the request.
- Transaction type determines whether it is income or expense.
- Reports can treat expenses as positive totals or negative signed values internally, but the API response should clearly expose `incomeTotal`, `expenseTotal`, and `netTotal`.
- Description is required.
- CategoryId is required.
- Date is required.

### Category

A category should have:

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Unique category identifier |
| `Name` | `string` | Required category name |
| `TransactionType` | enum or string | Values: `Income`, `Expense`, `Both` |

Example categories:

- Salary — Income
- Groceries — Expense
- Transport — Expense
- Entertainment — Expense
- Utilities — Expense

## Repository Interfaces

Create repository interfaces.

Suggested interfaces:

```csharp
public interface ITransactionRepository
{
    IReadOnlyCollection<Transaction> GetAll();
    Transaction? GetById(Guid id);
    Transaction Add(Transaction transaction);
    bool Delete(Guid id);
}
```

```csharp
public interface ICategoryRepository
{
    IReadOnlyCollection<Category> GetAll();
    Category? GetById(Guid id);
    Category Add(Category category);
}
```

Adjust names and namespaces to match the existing project structure.

## In-Memory Implementations

Create:

- `InMemoryTransactionRepository`
- `InMemoryCategoryRepository`

Implementation requirements:

- Use private `List<T>` storage.
- Seed initial categories and transactions.
- Use deterministic GUIDs for seeded data so tests can rely on them.
- Do not use static mutable state unless there is a strong reason.
- Register repositories as singletons in dependency injection so data added through API calls remains available during the app lifetime.
- Keep repository methods simple and synchronous unless the existing project already uses async patterns.

## Seed Data Requirements

Seed categories:

| Name | Transaction Type |
|---|---|
| Salary | Income |
| Groceries | Expense |
| Transport | Expense |
| Entertainment | Expense |
| Utilities | Expense |

Seed transactions:

| Date | Category | Type | Amount | Description |
|---|---|---|---:|---|
| 2026-05-01 | Salary | Income | 1200.00 | Monthly salary |
| 2026-05-04 | Transport | Expense | 14.20 | Uber Trip |
| 2026-05-05 | Groceries | Expense | 32.10 | Silpo Market |
| 2026-05-06 | Entertainment | Expense | 9.99 | Netflix Subscription |
| 2026-05-10 | Utilities | Expense | 65.00 | Electricity Bill |

Use safe fake data only. Do not include real personal data, account numbers, emails, bank identifiers, or secrets.

## API Endpoint Requirements

Create or update endpoints:

```http
GET    /api/transactions
POST   /api/transactions
GET    /api/transactions/{id}
DELETE /api/transactions/{id}

GET    /api/categories
POST   /api/categories
```

## DTO Requirements

Use request/response DTOs instead of exposing internal mutable models directly.

Suggested transaction create request:

```json
{
  "date": "2026-05-07",
  "description": "Coffee",
  "amount": 4.50,
  "transactionType": "Expense",
  "categoryId": "CATEGORY_GUID_HERE"
}
```

Suggested transaction response:

```json
{
  "id": "TRANSACTION_GUID_HERE",
  "date": "2026-05-07",
  "description": "Coffee",
  "amount": 4.50,
  "transactionType": "Expense",
  "category": {
    "id": "CATEGORY_GUID_HERE",
    "name": "Groceries"
  }
}
```

## Validation Requirements

Implement basic validation:

- Transaction amount must be greater than zero.
- Description is required.
- Date is required.
- Transaction type must be valid.
- Category must exist.
- Category must be compatible with transaction type unless category type is `Both`.
- Category name is required.
- Duplicate category names should be rejected case-insensitively.

Use simple validation in controllers or services. Do not add heavy validation frameworks unless already present.

## Tests

Add or update tests using xUnit and FluentAssertions.

Required tests:

1. Seeded categories are available.
2. Seeded transactions are available.
3. Adding a valid transaction stores it in memory.
4. Adding a transaction with an unknown category fails.
5. Adding a transaction with amount less than or equal to zero fails.
6. Adding a duplicate category name fails.
7. Deleting an existing transaction succeeds.
8. Deleting a missing transaction returns not found or false, depending on API style.

## Dependency Injection

Register repositories in `Program.cs`.

Expected style:

```csharp
builder.Services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
```

Ensure seeded data is available after the app starts.

## Documentation Updates

Update `README.md` only if needed to ensure it still says:

- The API uses seeded in-memory repositories.
- Data resets on restart.
- No database setup is required.

## Output Requirements

After implementing, summarize:

- Created files
- Updated files
- Seeded data
- API endpoints added or changed
- Tests added
- Any assumptions made

Do not add SQL Server, EF Core, Docker, LocalDB, migrations, authentication, frontend UI, or multi-user functionality.
