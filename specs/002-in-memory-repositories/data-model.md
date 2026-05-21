# Data Model: Seeded In-Memory Repositories

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

This feature does not introduce new domain entities — those landed in feature 001 ([Transaction](../../src/backend/FinanceTracker/Finance.Data/Models/Transaction.cs), [Category](../../src/backend/FinanceTracker/Finance.Data/Models/Category.cs), [TransactionType](../../src/backend/FinanceTracker/Finance.Data/Models/TransactionType.cs), [CategoryType](../../src/backend/FinanceTracker/Finance.Data/Models/CategoryType.cs)). What this document specifies is the **repository contract** that wraps those entities, the **in-memory implementation outline**, the **seed data**, and the **DI wiring**.

## 1. Repository interfaces

Both interfaces live in `src/backend/FinanceTracker/Finance.Data/Repositories/` under namespace `Finance.Data.Repositories`.

### `ITransactionRepository`

```csharp
namespace Finance.Data.Repositories;

using Finance.Data.Models;

public interface ITransactionRepository
{
    IReadOnlyCollection<Transaction> GetAll();
    Transaction? GetById(int id);
    Transaction Add(Transaction transaction);
    bool Delete(int id);
}
```

Semantics:

- **`GetAll`** — returns every stored transaction in insertion order (seeded records first, then runtime adds in the order they were accepted). The returned collection is a snapshot view; callers MUST treat it as read-only. The implementation returns `_transactions.AsReadOnly()` or the equivalent so consumers cannot mutate the backing list.
- **`GetById`** — returns the matching record or `null` if no transaction has the given id. Never throws on miss. ([research.md §5](research.md#5-getbyid-return-idiom-and-delete-return-idiom))
- **`Add`** — assigns the next available id (see [research.md §2](research.md#2-identifier-generation-strategy)), appends the resulting record to the store, and returns the stored form. The input transaction's `Id` field is ignored — the repository is the authority on identity. Callers SHOULD construct their input with `Id = 0` (or any sentinel) and read the assigned id off the return value. The repository does NOT validate `Amount`, `CategoryIds` membership, or `Type` ↔ `CategoryType` compatibility — those are Business-layer concerns ([research.md §6](research.md#6-where-validation-lives)).
- **`Delete`** — removes the record with the given id from the store. Returns `true` on success, `false` if no record had that id. Never throws.

### `ICategoryRepository`

```csharp
namespace Finance.Data.Repositories;

using Finance.Data.Models;

public interface ICategoryRepository
{
    IReadOnlyCollection<Category> GetAll();
    Category? GetById(int id);
    Category Add(Category category);
}
```

Semantics:

- **`GetAll`** and **`GetById`** — same shape and semantics as the transaction repository.
- **`Add`** — assigns the next available id, then checks the existing names with `StringComparer.OrdinalIgnoreCase`. If a match is found, the call throws `InvalidOperationException` with the message `$"A category named '{category.Name}' already exists."` and does NOT increment the id counter. If no match is found, the candidate is appended and the stored form is returned. The input `Id` is ignored, same as transactions. **No `Delete` method** — categories are append-only by user direction (see spec clarification).

**Why an exception, not a `Category?` return**: The duplicate-name rejection is a programming/validation error from the caller's perspective, not a normal-flow miss. The future controllers feature will catch this in the conflict path and map it to HTTP 409. A nullable return would conflate "duplicate" with the standard "not found" idiom and would require every caller to disambiguate.

## 2. In-memory implementations

Both implementations live in the same `Finance.Data/Repositories/` folder and namespace.

### `InMemoryTransactionRepository`

```csharp
namespace Finance.Data.Repositories;

using Finance.Data.Models;

public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions;
    private int _nextId;

    public InMemoryTransactionRepository()
    {
        _transactions = new List<Transaction>
        {
            new(1, new DateTime(2026, 5,  1, 12, 0, 0), "Monthly salary",        1200.00m, TransactionType.Income,  new[] { 1 }),
            new(2, new DateTime(2026, 5,  4, 12, 0, 0), "Uber Trip",               14.20m, TransactionType.Expense, new[] { 3 }),
            new(3, new DateTime(2026, 5,  5, 12, 0, 0), "Silpo Market",            32.10m, TransactionType.Expense, new[] { 2 }),
            new(4, new DateTime(2026, 5,  6, 12, 0, 0), "Netflix Subscription",     9.99m, TransactionType.Expense, new[] { 4 }),
            new(5, new DateTime(2026, 5, 10, 12, 0, 0), "Electricity Bill",        65.00m, TransactionType.Expense, new[] { 5 }),
        };
        _nextId = 6;
    }

    public IReadOnlyCollection<Transaction> GetAll() => _transactions.AsReadOnly();

    public Transaction? GetById(int id) => _transactions.FirstOrDefault(t => t.Id == id);

    public Transaction Add(Transaction transaction)
    {
        var stored = transaction with { Id = _nextId };
        _transactions.Add(stored);
        _nextId++;
        return stored;
    }

    public bool Delete(int id)
    {
        var existing = _transactions.FirstOrDefault(t => t.Id == id);
        if (existing is null) return false;
        _transactions.Remove(existing);
        return true;
    }
}
```

Notes:

- Category-id arrays (`new[] { 1 }`) are stored as `int[]`. `Transaction.CategoryIds` is typed `IReadOnlyList<int>`; `int[]` satisfies that interface and is the lightest-weight option for a single-element seed.
- `with { Id = _nextId }` is the canonical record-positional rewrite for assigning the repository-owned id. No mutation of the input occurs.
- `_nextId++` is the **last** statement in `Add`'s success path. Per [research.md §2](research.md#2-identifier-generation-strategy), this gates id consumption on success — though for `InMemoryTransactionRepository` there is currently no rejection path. The pattern is kept for symmetry with the category repository.

### `InMemoryCategoryRepository`

```csharp
namespace Finance.Data.Repositories;

using Finance.Data.Models;

public sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly List<Category> _categories;
    private int _nextId;

    public InMemoryCategoryRepository()
    {
        _categories = new List<Category>
        {
            new(1, "Salary",        CategoryType.Income),
            new(2, "Groceries",     CategoryType.Expense),
            new(3, "Transport",     CategoryType.Expense),
            new(4, "Entertainment", CategoryType.Expense),
            new(5, "Utilities",     CategoryType.Expense),
        };
        _nextId = 6;
    }

    public IReadOnlyCollection<Category> GetAll() => _categories.AsReadOnly();

    public Category? GetById(int id) => _categories.FirstOrDefault(c => c.Id == id);

    public Category Add(Category category)
    {
        if (_categories.Any(c => string.Equals(c.Name, category.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A category named '{category.Name}' already exists.");
        }

        var stored = category with { Id = _nextId };
        _categories.Add(stored);
        _nextId++;
        return stored;
    }
}
```

Notes:

- The duplicate-name check runs **before** the id is consumed (the `_nextId++` only happens on the success path). This is the mechanism that delivers spec SC-005 ("never causes a gap in the assigned identifier sequence").
- `StringComparer.OrdinalIgnoreCase` is the canonical .NET case-insensitive comparer for names that are not culture-sensitive (no Turkish dotted/dotless I concerns expected for category names like "Groceries" or "Utilities"). Using `StringComparison.OrdinalIgnoreCase` inline avoids a static comparer field.

## 3. Seed data — canonical reference

### Categories (5)

| Id | Name | Type |
|----|------|------|
| 1 | Salary | `Income` |
| 2 | Groceries | `Expense` |
| 3 | Transport | `Expense` |
| 4 | Entertainment | `Expense` |
| 5 | Utilities | `Expense` |

### Transactions (5)

| Id | Timestamp | Description | Amount | Type | CategoryIds |
|----|-----------|-------------|-------:|------|-------------|
| 1 | 2026-05-01 12:00:00 | "Monthly salary" | 1200.00 | `Income` | `[1]` |
| 2 | 2026-05-04 12:00:00 | "Uber Trip" | 14.20 | `Expense` | `[3]` |
| 3 | 2026-05-05 12:00:00 | "Silpo Market" | 32.10 | `Expense` | `[2]` |
| 4 | 2026-05-06 12:00:00 | "Netflix Subscription" | 9.99 | `Expense` | `[4]` |
| 5 | 2026-05-10 12:00:00 | "Electricity Bill" | 65.00 | `Expense` | `[5]` |

All amounts are USD (no currency field; constitution Principle III). All timestamps use noon local time ([research.md §1](research.md#1-time-of-day-component-of-seeded-transaction-timestamps)).

## 4. Dependency injection

`Finance.Api/Program.cs` adds two lines, after `builder.Services.AddControllers();`:

```csharp
builder.Services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
```

And one `using` directive at the top:

```csharp
using Finance.Data.Repositories;
```

The order of the two registrations is not significant — neither implementation depends on the other through DI; their seed data references each other only by integer id, which is a value-level reference, not a DI graph reference. Order is purely cosmetic.

**Singleton lifetime** is the load-bearing decision (constitution Principle III). `AddTransient` or `AddScoped` would silently re-seed on every request and lose every runtime add — which is exactly the failure mode the constitution warns about. Tests SHOULD include a regression check that resolves `ITransactionRepository` twice from the same provider and asserts the two references are the same instance.

## 5. Tests

All new tests live in `src/backend/FinanceTracker/tests/Finance.Data.UnitTests/Repositories/`. The project already has a `<ProjectReference>` to `Finance.Data`; no csproj changes are needed.

Test files:

### `InMemoryCategoryRepositoryTests.cs` (~5 tests)

| Test | Maps to |
|------|---------|
| `GetAll_returns_five_seeded_categories_in_id_order` | spec US1 / acceptance scenario 2 |
| `GetById_returns_seeded_category_by_literal_id` | spec US1 / FR-003 |
| `GetById_returns_null_for_unknown_id` | spec US3 / FR-010 |
| `Add_appends_new_category_and_assigns_next_id` | spec US2 / FR-006 |
| `Add_rejects_duplicate_name_case_insensitively_without_consuming_id` | spec US2 / FR-007 / SC-005 |

### `InMemoryTransactionRepositoryTests.cs` (~7 tests)

| Test | Maps to |
|------|---------|
| `GetAll_returns_five_seeded_transactions_in_id_order` | spec US1 / acceptance scenario 1 |
| `Seeded_transactions_reference_seeded_categories_only` | spec FR-002 |
| `GetById_returns_seeded_transaction_by_literal_id` | spec US3 / acceptance scenario 1 |
| `GetById_returns_null_for_unknown_id` | spec US3 / acceptance scenario 2 / FR-010 |
| `Add_appends_new_transaction_and_assigns_id_six` | spec US2 / FR-004 / FR-005 |
| `Delete_existing_returns_true_and_removes_record` | spec US3 / acceptance scenario 3 |
| `Delete_missing_returns_false_and_does_not_throw` | spec US3 / acceptance scenario 4 |
| `Adds_one_hundred_transactions_with_reads_under_fifty_milliseconds` | spec SC-004 |

### Cross-repository DI test (in `InMemoryTransactionRepositoryTests.cs` or a small dedicated file)

| Test | Maps to |
|------|---------|
| `Resolving_repository_twice_from_same_provider_yields_same_instance` | spec FR-008 / constitution Principle III (singleton lifetime) |

All tests use `Xunit.Assert` (`Assert.Equal`, `Assert.Null`, `Assert.NotNull`, `Assert.True`, `Assert.False`, `Assert.Collection`, `Assert.Throws<InvalidOperationException>`). No FluentAssertions.

The perf smoke test uses `System.Diagnostics.Stopwatch`. The assertion is on the *total* elapsed time of 100 inserts plus 100 reads, with a generous upper bound (e.g., `Assert.True(elapsed < TimeSpan.FromSeconds(1));`) — the 50 ms-per-read SC-004 bar is by design easy to clear; the test exists to catch a future change that accidentally introduces O(n²) behavior, not to measure latency precisely.

## 6. What this feature does NOT introduce

- No domain entities (those are owned by feature 001).
- No DTOs (those are owned by feature 001; this feature does not consume them either).
- No HTTP endpoints / controllers (deferred).
- No business validation (`Amount > 0`, category existence checks, category↔type compatibility) — deferred ([research.md §6](research.md#6-where-validation-lives)).
- No update operation on either repository — categories are append-only (spec clarification), transactions have no update in the ai-artifacts spec either and the report features don't need one.
- No NuGet package additions.

These omissions are deliberate scope discipline, not gaps.
