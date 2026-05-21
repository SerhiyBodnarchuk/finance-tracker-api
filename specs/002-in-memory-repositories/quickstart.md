# Quickstart: Seeded In-Memory Repositories

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/repository-contracts.md](contracts/repository-contracts.md)

## What this feature delivers

When this feature is implemented, you can:

1. **Build the solution** without any new package downloads.
2. **Resolve `ITransactionRepository` and `ICategoryRepository`** from the `Finance.Api` host's DI container and find the seeded data already loaded.
3. **Add a new transaction or category at runtime**, see it persist for the lifetime of the process, and observe that it's gone after a restart.
4. **Run the new `Finance.Data.UnitTests/Repositories/` tests** and have them all pass.

Note: there are still no HTTP endpoints for transactions or categories in this feature. To exercise the repositories from a controller you need feature 003 (controllers) to ship. Until then, the test suite is the demonstration vehicle.

## Build, run, test

From the solution directory:

```powershell
cd src/backend/FinanceTracker
dotnet restore
dotnet build
dotnet test
```

To run the API host (no transaction endpoints exist yet, but the app starts cleanly and `/scalar/v1` shows the OpenAPI document):

```powershell
dotnet run --project Finance.Api
```

To run only this feature's tests:

```powershell
dotnet test --filter "FullyQualifiedName~Finance.Data.UnitTests.Repositories"
```

To run a single test:

```powershell
dotnet test --filter "FullyQualifiedName~InMemoryCategoryRepositoryTests.Add_rejects_duplicate_name_case_insensitively_without_consuming_id"
```

## Verification checklist (after implementation)

Use this as a smoke test in PR review:

- [ ] `Finance.Data/Repositories/ITransactionRepository.cs` and `ICategoryRepository.cs` exist and declare the four (resp. three) members documented in [contracts/repository-contracts.md](contracts/repository-contracts.md).
- [ ] `Finance.Data/Repositories/InMemoryTransactionRepository.cs` and `InMemoryCategoryRepository.cs` implement the interfaces exactly as outlined in [data-model.md §2](data-model.md#2-in-memory-implementations).
- [ ] Five seed categories with ids 1–5 are loaded in the constructor; five seed transactions with ids 1–5 are loaded in the constructor; `_nextId` is initialized to `6` in both repositories.
- [ ] `InMemoryCategoryRepository.Add` performs the case-insensitive name check **before** incrementing `_nextId`.
- [ ] `Finance.Api/Program.cs` contains the two `AddSingleton<...>()` registrations from [data-model.md §4](data-model.md#4-dependency-injection) and a `using Finance.Data.Repositories;` directive.
- [ ] `tests/Finance.Data.UnitTests/Repositories/InMemoryCategoryRepositoryTests.cs` and `InMemoryTransactionRepositoryTests.cs` exist, use xUnit v3, and use only `Xunit.Assert` for assertions. No `FluentAssertions` package reference appears in the test csproj.
- [ ] `dotnet test` runs to completion in under 5 seconds (spec SC-002).
- [ ] All Principle IV "Repository behaviour" tests pass.
- [ ] No new NuGet package references are added to any csproj.

## Sanity-check session (manual)

After the feature ships, the following short scripted session (using LINQPad or a quick `csi` REPL referencing `Finance.Data.dll`) is the fastest way to convince yourself the contract works:

```csharp
ICategoryRepository categories = new InMemoryCategoryRepository();
ITransactionRepository transactions = new InMemoryTransactionRepository();

// US1: seeded data is available
categories.GetAll().Count;   // 5
transactions.GetAll().Count; // 5
categories.GetById(1)?.Name; // "Salary"
transactions.GetById(5)?.Description; // "Electricity Bill"

// US2: add a category, then a transaction tagged with it
var savings = categories.Add(new Category(0, "Savings", CategoryType.Income));
savings.Id;  // 6

var paycheck = transactions.Add(new Transaction(
    Id: 0,
    Timestamp: new DateTime(2026, 5, 31, 9, 0, 0),
    Description: "Bonus",
    Amount: 250m,
    Type: TransactionType.Income,
    CategoryIds: new[] { savings.Id }));
paycheck.Id; // 6

// FR-007: case-insensitive duplicate name rejected, no id hole
try { categories.Add(new Category(0, "GROCERIES", CategoryType.Expense)); }
catch (InvalidOperationException) { }
// next legitimate add still gets id 7, not 8
var transport2 = categories.Add(new Category(0, "Online Subscriptions", CategoryType.Expense));
transport2.Id; // 7

// US3: delete + miss
transactions.Delete(paycheck.Id); // true
transactions.GetById(paycheck.Id); // null
transactions.Delete(paycheck.Id); // false (no throw)
```

If any of those lines fail to produce the indicated value, the implementation has diverged from [contracts/repository-contracts.md](contracts/repository-contracts.md) and the cause should be tracked down before merging.

## What to do next (after this feature merges)

The natural follow-on feature is **transactions and categories HTTP endpoints + the business-layer validators**. That feature:

- Adds `TransactionsController` and `CategoriesController` in `Finance.Api/Controllers/`.
- Adds business-layer validators that own `Amount > 0`, "every `CategoryIds` entry resolves to an existing category", and the multi-category compatibility rule with `TransactionType`.
- Maps `InvalidOperationException` from `ICategoryRepository.Add` to HTTP 409, `null` from `GetById` to HTTP 404, `false` from `Delete` to HTTP 404.

That feature should not require any changes to this feature's surface; if it does, this feature's contract was probably wrong and the diff should be reviewed against [contracts/repository-contracts.md](contracts/repository-contracts.md) before changing the repository code.
