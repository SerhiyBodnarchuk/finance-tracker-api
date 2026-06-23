# Code Conventions

## Architecture Boundaries

- **NEVER** inject `ITransactionRepository` or `ICategoryRepository` directly into controllers or validators — all access must go through Business services (`ITransactionService`, `ICategoryService`, `IReportService`).
- **NEVER** return a domain entity (`Transaction`, `Category`) from a service method — map to the corresponding DTO before the result crosses the layer boundary.
- **NEVER** reference `Finance.Business` or `Finance.Api` from `Finance.Data` — dependency direction is one-way: Api → Business → Data.

## Domain Rules

- **ALWAYS** keep `Transaction.CategoryIds` non-empty — a transaction with zero categories is invalid.
- **ALWAYS** validate that every category attached to a transaction is type-compatible with its `TransactionType` (Income allows Income/Both; Expense allows Expense/Both).
- **ALWAYS** use `int` for entity identifiers — never `Guid`.
- **NEVER** store or return a negative `Amount` from a request — direction is encoded in `TransactionType`, not the sign.

## Report Aggregation

- **NEVER** split or pro-rate a transaction across its categories — a multi-category transaction contributes its full signed amount to each breakdown item.
- **ALWAYS** treat Period report date ranges as fully inclusive: `[start 00:00:00, end 23:59:59]`.
- **ALWAYS** sort `categoryBreakdown` income-side first (positive totals, zero-total Both-type categories here), then expense-side (negative totals), by category name ascending within each group.
- **NEVER** persist, cache, or store a `ReportResult` — every report is computed fresh per request.

## DI Lifetimes

- **ALWAYS** register repositories as singletons — they carry all process-wide state.
- **ALWAYS** register services, strategies, the factory, and validators as scoped.
- **NEVER** inject a scoped service into a singleton.

## Testing

- **NEVER** use FluentAssertions — use `Xunit.Assert` API only (`Assert.Equal`, `Assert.True`, `Assert.Throws<T>`, `Assert.Collection`, etc.).
- **ALWAYS** use strict-mode Moq mocks in unit test projects.
- **ALWAYS** mirror the production folder structure 1:1 in test projects (e.g. `Finance.Business/Services/CategoryService.cs` → `Finance.Business.UnitTests/Services/CategoryServiceTests.cs`).
- **NEVER** reference production repositories directly from integration tests — use `WebApplicationFactory<Program>`.

## OpenAPI / Serialization

- **NEVER** add Swashbuckle — the project uses `Microsoft.AspNetCore.OpenApi` (built-in) + Scalar.
- **ALWAYS** ensure new enum types get string-typed schema treatment — the schema transformer in `Program.cs` handles this automatically for all enums, but new enums must remain standard C# `enum` types (not classes) for the transformer to detect them.
- **NEVER** expose the OpenAPI or Scalar endpoints outside `IsDevelopment()`.
