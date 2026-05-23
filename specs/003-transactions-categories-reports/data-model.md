# Data Model: Transactions / Categories Endpoints with Business Validation and Period Reports

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

This feature does not introduce new **persisted** data — the entities (`Transaction`, `Category`) and DTOs (`TransactionCreateRequest`, `TransactionResponse`, `CategoryCreateRequest`, `CategoryResponse`, `ReportRequest`, `PeriodReportData`, `ReportResult`, `CategoryBreakdownItem`) all exist from feature 001. What this document specifies is the **service surface** the feature introduces: validators in `Finance.Business/Validation/`, the report-system services in `Finance.Business/Reports/`, the three controllers in `Finance.Api/Controllers/`, and the DI wiring in `Program.cs`.

## 1. Validation result types

Located in `Finance.Business/Validation/`.

### `ValidationError`

```csharp
namespace Finance.Business.Validation;

public sealed record ValidationError(string Field, string Message);
```

- `Field` — the JSON-path-ish name of the offending field (e.g., `"amount"`, `"categoryIds"`, `"categoryIds[0]"`, `"name"`). Lowercase, matching the on-the-wire JSON property name.
- `Message` — a human-readable reason. Stable for the same failure mode (tests can assert on substrings, not exact text).

### `ValidationResult`

```csharp
namespace Finance.Business.Validation;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<ValidationError>());

    public static ValidationResult Failure(params ValidationError[] errors) =>
        new(false, errors);
}
```

- `IsValid == true` ⇔ `Errors.Count == 0` (by construction; the controller MAY rely on this).
- `Success` is shared (zero-allocation happy path).
- `Failure(...)` is the standard construction site; takes a `params` array for one-or-more errors.

## 2. Validator services

### `ITransactionValidator`

```csharp
namespace Finance.Business.Validation;

using Finance.Business.Dtos.Transactions;

public interface ITransactionValidator
{
    ValidationResult ValidateForCreate(TransactionCreateRequest request);
}
```

### `TransactionValidator`

```csharp
namespace Finance.Business.Validation;

using Finance.Business.Dtos.Transactions;
using Finance.Data.Models;
using Finance.Data.Repositories;

public sealed class TransactionValidator(ICategoryRepository categories) : ITransactionValidator
{
    public ValidationResult ValidateForCreate(TransactionCreateRequest request)
    {
        var errors = new List<ValidationError>();

        if (request.Amount <= 0m)
            errors.Add(new ValidationError("amount", "Amount must be greater than zero."));

        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add(new ValidationError("description", "Description is required."));

        if (request.CategoryIds is null || request.CategoryIds.Count == 0)
        {
            errors.Add(new ValidationError("categoryIds", "At least one category id is required."));
            // Short-circuit referential / type checks; nothing to resolve.
            return errors.Count > 0 ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
        }

        var resolved = new List<Category>(request.CategoryIds.Count);
        for (var i = 0; i < request.CategoryIds.Count; i++)
        {
            var id = request.CategoryIds[i];
            var category = categories.GetById(id);
            if (category is null)
                errors.Add(new ValidationError($"categoryIds[{i}]", $"Category id {id} does not exist."));
            else
                resolved.Add(category);
        }

        if (resolved.Count == request.CategoryIds.Count)
        {
            for (var i = 0; i < resolved.Count; i++)
            {
                if (!IsCompatible(request.Type, resolved[i].Type))
                {
                    errors.Add(new ValidationError(
                        $"categoryIds[{i}]",
                        $"Category '{resolved[i].Name}' ({resolved[i].Type}) is not compatible with transaction type {request.Type}."));
                }
            }
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
    }

    private static bool IsCompatible(TransactionType transactionType, CategoryType categoryType) =>
        categoryType == CategoryType.Both ||
        (transactionType == TransactionType.Income  && categoryType == CategoryType.Income) ||
        (transactionType == TransactionType.Expense && categoryType == CategoryType.Expense);
}
```

Notes:

- Uses the C# 12 primary-constructor syntax — keeps the validator a one-field service with no boilerplate.
- The compatibility check runs **only** when every category id resolved. Mixing "unknown id" with "incompatible type" errors in the same response would be noisy and might point a caller at the wrong fix.
- The `categoryIds[i]` field-path syntax in `ValidationError.Field` mirrors what `ValidationProblemDetails`'s `errors` dictionary key uses, so the controller's mapper is a one-liner.

### `ICategoryValidator`

```csharp
namespace Finance.Business.Validation;

using Finance.Business.Dtos.Categories;

public interface ICategoryValidator
{
    ValidationResult ValidateForCreate(CategoryCreateRequest request);
}
```

### `CategoryValidator`

```csharp
namespace Finance.Business.Validation;

using Finance.Business.Dtos.Categories;

public sealed class CategoryValidator : ICategoryValidator
{
    public ValidationResult ValidateForCreate(CategoryCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationResult.Failure(
                new ValidationError("name", "Name is required."));
        }

        return ValidationResult.Success;
    }
}
```

Notes:

- The duplicate-name check is **NOT** in this validator. That check lives in `InMemoryCategoryRepository.Add` (case-insensitive name uniqueness, established in feature 002). The repository throws `InvalidOperationException` on duplicate; the controller catches that and returns HTTP 409.
- The `Type` field is the `CategoryType` enum; an invalid string is rejected at the JSON model-binding stage with HTTP 400 automatically — no validator code needed.

## 3. Report-system services

Located in `Finance.Business/Reports/`.

### `IReportStrategy`

```csharp
namespace Finance.Business.Reports;

using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;

public interface IReportStrategy
{
    ReportType Type { get; }

    ReportResult Generate(ReportRequest request);
}
```

### `IReportStrategyFactory`

```csharp
namespace Finance.Business.Reports;

using Finance.Business.Enums;

public interface IReportStrategyFactory
{
    IReportStrategy? TryGet(ReportType type);
}
```

### `ReportStrategyFactory`

```csharp
namespace Finance.Business.Reports;

using Finance.Business.Enums;

public sealed class ReportStrategyFactory : IReportStrategyFactory
{
    private readonly IReadOnlyDictionary<ReportType, IReportStrategy> _strategies;

    public ReportStrategyFactory(IEnumerable<IReportStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Type);
    }

    public IReportStrategy? TryGet(ReportType type) =>
        _strategies.TryGetValue(type, out var strategy) ? strategy : null;
}
```

Notes:

- Dictionary is built once at construction time from the DI-resolved `IEnumerable<IReportStrategy>`.
- A future feature that registers `IsoWeekReportStrategy` causes it to appear in the dictionary automatically; the factory does not need editing.
- If two strategies advertise the same `ReportType` (would be a programmer error), `ToDictionary` throws at startup — fail-fast.

### `ReportValidationException`

```csharp
namespace Finance.Business.Reports;

using Finance.Business.Validation;

public sealed class ReportValidationException(IReadOnlyList<ValidationError> errors)
    : Exception($"Report request is invalid: {errors.Count} error(s).")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
```

The single narrow exception type used by report strategies to surface validation failures. The controller catches **exactly** this type — not `Exception` in general — and maps to HTTP 400.

### `PeriodReportStrategy`

```csharp
namespace Finance.Business.Reports;

using System.Globalization;
using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Validation;
using Finance.Data.Models;
using Finance.Data.Repositories;

public sealed class PeriodReportStrategy(
    ITransactionRepository transactions,
    ICategoryRepository categories)
    : IReportStrategy
{
    public ReportType Type => ReportType.Period;

    public ReportResult Generate(ReportRequest request)
    {
        var data = ParseAndValidate(request);

        var rangeStart = data.Start.ToDateTime(TimeOnly.MinValue);                  // [00:00:00]
        var rangeEnd   = data.End.ToDateTime(new TimeOnly(23, 59, 59));             // [23:59:59]

        var all = transactions.GetAll();
        var inWindow = new List<Transaction>(all.Count);
        foreach (var t in all)
        {
            if (t.Timestamp >= rangeStart && t.Timestamp <= rangeEnd)
                inWindow.Add(t);
        }

        var incomeTotal = 0m;
        var expenseTotal = 0m;
        foreach (var t in inWindow)
        {
            if (t.Type == TransactionType.Income) incomeTotal  += t.Amount;
            else                                  expenseTotal += t.Amount;
        }
        var netTotal = incomeTotal - expenseTotal;

        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var breakdownTotals = new Dictionary<int, decimal>();
        foreach (var t in inWindow)
        {
            var signed = t.Type == TransactionType.Income ? t.Amount : -t.Amount;
            foreach (var categoryId in t.CategoryIds)
            {
                breakdownTotals[categoryId] = breakdownTotals.TryGetValue(categoryId, out var current)
                    ? current + signed
                    : signed;
            }
        }

        var breakdown = breakdownTotals
            .Select(kvp => new CategoryBreakdownItem(categoryNames[kvp.Key], kvp.Value))
            // Income-side first (total >= 0), then expense-side (total < 0); alphabetical within each group.
            .OrderBy(item => item.Total < 0m)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToList();

        var period = $"{data.Start:yyyy-MM-dd}..{data.End:yyyy-MM-dd}";

        return new ReportResult(
            ReportType.Period,
            period,
            incomeTotal,
            expenseTotal,
            netTotal,
            breakdown);
    }

    private static PeriodReportData ParseAndValidate(ReportRequest request)
    {
        PeriodReportData data;
        try
        {
            data = request.Data.Deserialize<PeriodReportData>(JsonSerializationOptions.Default)
                   ?? throw new JsonException("Period report data payload is required.");
        }
        catch (JsonException ex)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", $"Invalid period report data payload: {ex.Message}")
            });
        }

        var errors = new List<ValidationError>();
        if (data.Start == default)
            errors.Add(new ValidationError("data.start", "Start date is required."));
        if (data.End == default)
            errors.Add(new ValidationError("data.end", "End date is required."));
        if (errors.Count == 0 && data.Start > data.End)
            errors.Add(new ValidationError("data", "Start date must be less than or equal to end date."));

        if (errors.Count > 0)
            throw new ReportValidationException(errors);

        return data;
    }
}
```

Notes on the implementation outline:

- `PeriodReportData.Start / End` are `DateOnly`. `ToDateTime(TimeOnly.MinValue)` and `ToDateTime(new TimeOnly(23, 59, 59))` give the inclusive bounds the constitution requires.
- The two-loop layout (filter to `inWindow`, then sum totals, then sum per-category) is deliberately explicit rather than collapsed into one LINQ pipeline — it's easier to reason about and to step through in a debugger, and the multi-category attribution rule (each `categoryId` in `t.CategoryIds` gets the full signed amount) is visible at first glance.
- Sort order uses `OrderBy(item => item.Total < 0m)`: `false` (income-side, total ≥ 0) sorts before `true` (expense-side, total < 0). Zero-total `Both` categories sort with the income side, which matches the constitution.
- `StringComparer.Ordinal` for the alphabetical-within secondary sort — culture-insensitive, predictable across machines.
- The `period` string format `yyyy-MM-dd..yyyy-MM-dd` matches the constitution's documented descriptor format.

## 4. Controllers

Located in `Finance.Api/Controllers/`. Each controller is a thin transport adapter — no validation, no aggregation. All three inherit `ControllerBase` and are decorated with `[ApiController]` + `[Route("api/[controller]")]`.

### `TransactionsController`

```csharp
namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController(
    ITransactionRepository transactions,
    ICategoryRepository categories,
    ITransactionValidator validator) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TransactionResponse>> List() { /* GetAll + map */ }

    [HttpGet("{id:int}")]
    public ActionResult<TransactionResponse> GetById(int id) { /* GetById or 404 */ }

    [HttpPost]
    public ActionResult<TransactionResponse> Create([FromBody] TransactionCreateRequest request)
    {
        var result = validator.ValidateForCreate(request);
        if (!result.IsValid) return BadRequest(ToValidationProblem(result));

        var entity = request.ToEntity(assignedId: 0);
        var stored = transactions.Add(entity);
        var response = stored.ToResponse(categories.GetAll().ToDictionary(c => c.Id));
        return CreatedAtAction(nameof(GetById), new { id = stored.Id }, response);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) =>
        transactions.Delete(id) ? NoContent() : NotFoundProblem(id);
}
```

### `CategoriesController`

```csharp
namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController(
    ICategoryRepository categories,
    ICategoryValidator validator) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<CategoryResponse>> List() { /* GetAll + map */ }

    [HttpGet("{id:int}")]
    public ActionResult<CategoryResponse> GetById(int id) { /* GetById or 404 */ }

    [HttpPost]
    public ActionResult<CategoryResponse> Create([FromBody] CategoryCreateRequest request)
    {
        var result = validator.ValidateForCreate(request);
        if (!result.IsValid) return BadRequest(ToValidationProblem(result));

        try
        {
            var stored = categories.Add(request.ToEntity(assignedId: 0));
            return CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate category name",
                Detail = ex.Message
            });
        }
    }
}
```

### `ReportsController`

```csharp
namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController(IReportStrategyFactory factory) : ControllerBase
{
    [HttpPost]
    public ActionResult<ReportResult> Generate([FromBody] ReportRequest request)
    {
        var strategy = factory.TryGet(request.Type);
        if (strategy is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported report type",
                Detail = $"Report type '{request.Type}' is not supported."
            });
        }

        try
        {
            return Ok(strategy.Generate(request));
        }
        catch (ReportValidationException ex)
        {
            return BadRequest(ToValidationProblem(ex.Errors));
        }
    }
}
```

A small static helper `ProblemDetailsMappers.ToValidationProblem(ValidationResult)` / `ToValidationProblem(IReadOnlyList<ValidationError>)` lives next to the controllers (or in `Finance.Api/Infrastructure/`) and builds a `ValidationProblemDetails` whose `errors` dictionary maps each `Field` to a single-element string array of `Message`. This keeps the three controllers DRY.

## 5. DI registration

Lines to add to `Finance.Api/Program.cs` (after the two `AddSingleton` lines from feature 002, before `AddOpenApi()`):

```csharp
using Finance.Business.Reports;
using Finance.Business.Validation;

builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
builder.Services.AddSingleton<ICategoryValidator, CategoryValidator>();
builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();
builder.Services.AddSingleton<IReportStrategyFactory, ReportStrategyFactory>();
```

The `AddSingleton<IReportStrategy, PeriodReportStrategy>` pattern is the registration site that a future strategy feature will copy verbatim with its own type.

## 6. Tests

### `Finance.Business.UnitTests/Validation/`

#### `TransactionValidatorTests.cs` (~8 tests)

| Test | Maps to |
|------|---------|
| `Returns_success_for_valid_request` | spec FR-003 happy path |
| `Returns_failure_when_amount_is_zero` | FR-009 |
| `Returns_failure_when_amount_is_negative` | FR-009 |
| `Returns_failure_when_description_is_whitespace` | FR-010 |
| `Returns_failure_when_categoryIds_is_empty` | FR-011 |
| `Returns_failure_with_index_when_categoryId_does_not_exist` | FR-012 |
| `Returns_failure_when_income_transaction_references_expense_only_category` | FR-013 |
| `Allows_expense_transaction_with_Both_type_category` | FR-013 (compatibility positive case) |

#### `CategoryValidatorTests.cs` (~3 tests)

| Test | Maps to |
|------|---------|
| `Returns_success_for_valid_request` | FR-007 happy path |
| `Returns_failure_when_name_is_empty_or_whitespace` | FR-014 |
| `Returns_success_when_name_is_non_empty_after_trim` | FR-014 |

### `Finance.Business.UnitTests/Reports/`

#### `ReportStrategyFactoryTests.cs` (~3 tests)

| Test | Maps to |
|------|---------|
| `TryGet_returns_PeriodReportStrategy_for_Period` | FR-028 |
| `TryGet_returns_null_for_IsoWeek_when_no_strategy_registered` | FR-018 |
| `Constructor_throws_when_two_strategies_share_a_type` | factory-correctness invariant |

#### `PeriodReportStrategyTests.cs` (~7 tests)

| Test | Maps to |
|------|---------|
| `Generates_summary_for_full_seed_window_with_documented_numbers` | SC-003 (income 1200, expense 121.29, net 1078.71, 5 breakdown items) |
| `Returns_zero_totals_and_empty_breakdown_for_empty_window` | edge case: window with no transactions |
| `Includes_transaction_on_exact_start_boundary` | FR-021 (inclusive at `[start 00:00:00]`) |
| `Includes_transaction_on_exact_end_boundary` | FR-021 (inclusive at `[end 23:59:59]`) |
| `Multi_category_transaction_contributes_full_amount_to_each_category` | FR-025 / SC-005 |
| `Breakdown_sort_order_is_income_first_then_alphabetical` | FR-026 |
| `Throws_ReportValidationException_when_start_greater_than_end` | FR-019 (`start > end`) |

The "Includes transaction on exact start boundary" test is added because the implementation uses `Timestamp >= rangeStart && Timestamp <= rangeEnd` — the most likely off-by-one bug at the boundary is exclusive-vs-inclusive on the upper end. One test per boundary is cheap insurance.

### `Finance.Api.IntegrationTests/`

The project's csproj is modified once to add `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 and a `<ProjectReference>` to `Finance.Api`.

#### `ApiTestFixture.cs`

Shared `IClassFixture<>` wrapper around `WebApplicationFactory<Program>`. Exposes a configured `HttpClient` with `Accept: application/json` set.

#### `TransactionsEndpointsTests.cs` (~5 tests)

| Test | Maps to |
|------|---------|
| `GET_transactions_returns_200_with_five_seeded_records` | FR-001 / SC-001 |
| `GET_transactions_id_returns_200_for_seed_record` | FR-002 |
| `GET_transactions_id_returns_404_for_unknown_id` | FR-002 |
| `POST_transactions_returns_201_with_location_for_valid_body` | FR-003 |
| `DELETE_transactions_id_returns_204_then_404_on_second_call` | FR-004 |

#### `CategoriesEndpointsTests.cs` (~3 tests)

| Test | Maps to |
|------|---------|
| `GET_categories_returns_200_with_five_seeded_records` | FR-005 |
| `POST_categories_returns_201_for_valid_body` | FR-007 |
| `POST_categories_returns_409_for_case_insensitive_duplicate_name` | FR-008 |

#### `ReportsEndpointTests.cs` (~5 tests)

| Test | Maps to |
|------|---------|
| `POST_reports_period_returns_200_with_documented_seed_window_numbers` | SC-003 |
| `POST_reports_iso_week_returns_400_with_unsupported_type` | FR-018 |
| `POST_reports_unknown_type_returns_400` | FR-017 |
| `POST_reports_period_with_start_after_end_returns_400` | FR-019 / SC-005 |
| `POST_reports_period_twice_returns_byte_identical_bodies` | SC-004 |

All tests use `Xunit.Assert`. No FluentAssertions.

## 7. What this feature does NOT introduce

- No domain entities (those are owned by feature 001).
- No new DTOs (those are owned by feature 001).
- No new repositories or storage abstractions (feature 002 owns the storage layer).
- No JSON or CSV export endpoints — deferred per user direction.
- No `IsoWeekReportStrategy` — the enum value exists but the strategy ships with a later feature.
- No PUT / PATCH endpoints — only POST + DELETE for writes.
- No authentication, authorization, or rate limiting.

These omissions are deliberate scope discipline.
