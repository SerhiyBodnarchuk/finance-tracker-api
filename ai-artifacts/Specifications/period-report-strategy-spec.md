# AI Agent Specification: Initial Period Report Strategy

You are working in a .NET API-only repository named `finance-tracker-api`.

Implement the initial report system using factory + strategy. Add only the first report type: `period`.

This is intended to become the example pattern for a reusable AI skill that will later generate new report strategies, such as `isoWeek` and `month`.

## Project Constraints

- API-only project.
- Single-user scope.
- In-memory repositories already provide transactions and categories.
- No frontend.
- No authentication.
- No multi-user support.
- No SQL Server.
- No EF Core.
- No Docker.
- No LocalDB.
- No database migrations.
- Keep implementation small and clear.
- Use tests-first or add tests together with implementation.

## Goal

Add a report generation system where a single endpoint accepts a report type and parameters in the request body.

Initial endpoint:

```http
POST /api/reports
Content-Type: application/json
Accept: application/json
```

Initial supported report type:

```text
period
```

The `period` report aggregates transactions between a provided `from` date and `to` date.

## Request Contract

Create a generic report request contract.

Example request:

```json
{
  "type": "period",
  "parameters": {
    "from": "2026-05-01",
    "to": "2026-05-31"
  }
}
```

Recommended C# shape:

```csharp
public sealed record ReportRequest(
    string Type,
    JsonElement Parameters
);
```

Alternative strongly typed parameter handling is acceptable, but the request body must keep the generic `type` + `parameters` structure because future report strategies will use different parameter shapes.

## Response Contract

Create a common report response shape.

Example response:

```json
{
  "type": "period",
  "period": {
    "from": "2026-05-01",
    "to": "2026-05-31"
  },
  "currency": "USD",
  "incomeTotal": 1200.00,
  "expenseTotal": 121.29,
  "netTotal": 1078.71,
  "categoryBreakdown": [
    {
      "category": "Groceries",
      "transactionType": "Expense",
      "total": 32.10,
      "transactionCount": 1
    },
    {
      "category": "Transport",
      "transactionType": "Expense",
      "total": 14.20,
      "transactionCount": 1
    }
  ],
  "transactions": [
    {
      "id": "TRANSACTION_GUID_HERE",
      "date": "2026-05-05",
      "description": "Silpo Market",
      "amount": 32.10,
      "transactionType": "Expense",
      "category": "Groceries"
    }
  ]
}
```

## Aggregation Rules

For the selected period:

- Include transactions where `Date >= from` and `Date <= to`.
- `incomeTotal` is the sum of amounts for `Income` transactions.
- `expenseTotal` is the sum of amounts for `Expense` transactions.
- `netTotal = incomeTotal - expenseTotal`.
- `categoryBreakdown` groups matching transactions by category and transaction type.
- Each category breakdown item includes:
  - category name
  - transaction type
  - total
  - transaction count
- `transactions` includes all matching transactions.
- Sort transactions by date ascending, then description ascending.
- Sort category breakdown by transaction type, then category name.

Use positive amounts in the response for both income and expense entries. The transaction type explains the direction.

## Period Rules

For `period` parameters:

- `from` is required.
- `to` is required.
- `from` and `to` use ISO date format: `yyyy-MM-dd`.
- `from` must be less than or equal to `to`.
- The period is inclusive.
- Invalid or missing parameters should return `400 Bad Request`.
- Unsupported report type should return `400 Bad Request` or a clear validation error.

## Architecture Requirements

Implement report generation with factory + strategy.

Create these abstractions or equivalents:

```csharp
public interface IReportStrategy
{
    string Type { get; }

    ReportResult Generate(ReportRequest request);
}
```

```csharp
public interface IReportStrategyFactory
{
    IReportStrategy Create(string reportType);
}
```

Create implementation:

- `PeriodReportStrategy`
- `ReportStrategyFactory`

The strategy should depend on repositories or services required to read transactions and categories.

The controller should not contain aggregation logic. It should only:

1. Receive the request.
2. Ask the factory for the correct strategy.
3. Execute the strategy.
4. Return the result.

## Parameter Parsing

Keep parameter parsing inside the strategy or in a small helper dedicated to that strategy.

For `period`, parse:

```json
{
  "from": "2026-05-01",
  "to": "2026-05-31"
}
```

If parsing fails, return a clear validation error.

Avoid fragile string parsing scattered across controllers.

## API Endpoint

Add or update:

```http
POST /api/reports
```

Example request:

```json
{
  "type": "period",
  "parameters": {
    "from": "2026-05-01",
    "to": "2026-05-31"
  }
}
```

Expected behavior:

- `200 OK` with report result for valid period report.
- `400 Bad Request` for unsupported report type.
- `400 Bad Request` for missing `from`.
- `400 Bad Request` for missing `to`.
- `400 Bad Request` for invalid date format.
- `400 Bad Request` for `from > to`.

## Dependency Injection

Register all required services in `Program.cs`.

Expected style:

```csharp
builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();
builder.Services.AddSingleton<IReportStrategyFactory, ReportStrategyFactory>();
```

Use the actual lifetimes that fit the existing repository design. If repositories are singletons, singleton report strategies are acceptable if they do not store request-specific mutable state.

## Tests

Add tests using xUnit and FluentAssertions.

### Strategy / Unit Tests

1. `PeriodReportStrategy` includes transactions inside the inclusive date range.
2. `PeriodReportStrategy` excludes transactions outside the range.
3. `PeriodReportStrategy` calculates `incomeTotal`, `expenseTotal`, and `netTotal` correctly.
4. `PeriodReportStrategy` groups category breakdown correctly.
5. `PeriodReportStrategy` returns transactions sorted by date and description.
6. `PeriodReportStrategy` rejects missing `from`.
7. `PeriodReportStrategy` rejects missing `to`.
8. `PeriodReportStrategy` rejects invalid date format.
9. `PeriodReportStrategy` rejects `from > to`.

### Factory Tests

10. Factory resolves `period` strategy case-insensitively.
11. Factory rejects unsupported report type.

### API Tests

12. `POST /api/reports` returns `200 OK` for a valid period report.
13. `POST /api/reports` returns `400 Bad Request` for an unsupported report type.
14. `POST /api/reports` returns `400 Bad Request` for invalid parameters.

## Skill Pattern Notes

Add a short internal comment or documentation note explaining that `PeriodReportStrategy` is the first example of the report strategy pattern.

Future AI-generated report strategies should follow the same pattern:

1. Define expected parameters.
2. Parse and validate parameters inside the strategy.
3. Convert parameters into a concrete date range.
4. Aggregate transactions.
5. Return the common report result shape.
6. Add strategy tests.
7. Register the strategy with the factory.

Do not create `IsoWeekReportStrategy` yet. Only implement `period`.

## Output Requirements

After implementing, summarize:

- Created files
- Updated files
- New report endpoint
- Strategy/factory design
- Tests added
- Example request and response
- Any assumptions made

Do not add SQL Server, EF Core, Docker, LocalDB, migrations, authentication, frontend UI, or multi-user functionality.
