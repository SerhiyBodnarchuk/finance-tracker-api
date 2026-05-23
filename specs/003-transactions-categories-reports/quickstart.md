# Quickstart: Transactions / Categories Endpoints with Business Validation and Period Reports

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/http-endpoints.md](contracts/http-endpoints.md)

## What this feature delivers

When this feature is implemented:

1. **Build the solution**; the only new package downloaded is `Microsoft.AspNetCore.Mvc.Testing` for the integration-tests project.
2. **Hit eight new endpoints** under `/api/transactions`, `/api/categories`, `/api/reports`.
3. **See validation errors come back as RFC 7807 `ValidationProblemDetails`** with per-field error keys.
4. **See a deterministic period report** with the documented seed-window numbers.
5. **Run the new test suites** in `Finance.Business.UnitTests` and `Finance.Api.IntegrationTests` and have them all pass.
6. **Notice that `/WeatherForecast` is gone** from the OpenAPI document — the default scaffold was removed.

## Build, run, test

From the solution directory:

```powershell
cd src/backend/FinanceTracker
dotnet restore
dotnet build
dotnet test
```

To run the API host:

```powershell
dotnet run --project Finance.Api
```

Then open the Scalar UI at `https://localhost:7266/scalar/v1` (or your configured port — see `Properties/launchSettings.json`). Every endpoint added in this feature should appear with its request and response shapes documented from the OpenAPI document.

To run only this feature's tests:

```powershell
dotnet test --filter "FullyQualifiedName~Finance.Business.UnitTests.Validation|FullyQualifiedName~Finance.Business.UnitTests.Reports|FullyQualifiedName~Finance.Api.IntegrationTests"
```

To run a single test:

```powershell
dotnet test --filter "FullyQualifiedName~PeriodReportStrategyTests.Multi_category_transaction_contributes_full_amount_to_each_category"
```

## Sanity-check session (curl, against a running host)

Assuming the host is at `https://localhost:7266`:

```powershell
# US1 — Reads
curl -k https://localhost:7266/api/transactions | jq .
curl -k https://localhost:7266/api/categories | jq .
curl -k https://localhost:7266/api/transactions/3 | jq .
curl -k -o /dev/null -w "%{http_code}\n" https://localhost:7266/api/transactions/99999    # 404

# US2 — Writes with validation
# Valid create
curl -k -X POST https://localhost:7266/api/categories `
  -H "Content-Type: application/json" `
  -d '{"name":"Savings","type":"Income"}' `
  -i | head -20

# Duplicate name → 409
curl -k -X POST https://localhost:7266/api/categories `
  -H "Content-Type: application/json" `
  -d '{"name":"GROCERIES","type":"Expense"}' `
  -i | head -20

# Invalid transaction (amount: 0) → 400 with errors map
curl -k -X POST https://localhost:7266/api/transactions `
  -H "Content-Type: application/json" `
  -d '{"timestamp":"2026-05-31T12:00:00","description":"x","amount":0,"transactionType":"Expense","categoryIds":[2]}' `
  -i | head -25

# Type-compatibility mismatch (Expense txn tagged with Salary which is Income-only) → 400
curl -k -X POST https://localhost:7266/api/transactions `
  -H "Content-Type: application/json" `
  -d '{"timestamp":"2026-05-31T12:00:00","description":"x","amount":1,"transactionType":"Expense","categoryIds":[1]}' `
  -i | head -25

# Delete an existing transaction → 204, again → 404
curl -k -X DELETE https://localhost:7266/api/transactions/4 -o /dev/null -w "%{http_code}\n"   # 204
curl -k -X DELETE https://localhost:7266/api/transactions/4 -o /dev/null -w "%{http_code}\n"   # 404

# US3 — Period report over the full seed window
curl -k -X POST https://localhost:7266/api/reports `
  -H "Content-Type: application/json" `
  -d '{"type":"Period","data":{"start":"2026-05-01","end":"2026-05-31"}}' | jq .
# Expect: incomeTotal=1200.00, expenseTotal=121.29, netTotal=1078.71, 5 breakdown items in income-first/alpha order.

# Unsupported type → 400
curl -k -X POST https://localhost:7266/api/reports `
  -H "Content-Type: application/json" `
  -d '{"type":"Month","data":{}}' -i | head -15

# IsoWeek (enum value exists but strategy not implemented) → 400
curl -k -X POST https://localhost:7266/api/reports `
  -H "Content-Type: application/json" `
  -d '{"type":"IsoWeek","data":{"week":"2026-W19"}}' -i | head -15

# start > end → 400
curl -k -X POST https://localhost:7266/api/reports `
  -H "Content-Type: application/json" `
  -d '{"type":"Period","data":{"start":"2026-05-31","end":"2026-05-01"}}' -i | head -25
```

## Verification checklist (after implementation)

- [ ] `Finance.Business/Validation/` contains `ValidationResult.cs`, `ValidationError.cs`, `ITransactionValidator.cs`, `ICategoryValidator.cs`, `TransactionValidator.cs`, `CategoryValidator.cs`.
- [ ] `Finance.Business/Reports/` contains `IReportStrategy.cs`, `IReportStrategyFactory.cs`, `ReportStrategyFactory.cs`, `ReportValidationException.cs`, `PeriodReportStrategy.cs`.
- [ ] `Finance.Api/Controllers/` contains `TransactionsController.cs`, `CategoriesController.cs`, `ReportsController.cs`. The `WeatherForecastController.cs` and `WeatherForecast.cs` files are **gone**.
- [ ] `Finance.Api/Program.cs` registers the two validators, `PeriodReportStrategy`, and `ReportStrategyFactory` as singletons.
- [ ] `Finance.Api.IntegrationTests/Finance.Api.IntegrationTests.csproj` references `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 **and** has a `<ProjectReference>` to `Finance.Api`.
- [ ] `Finance.Business.UnitTests/Validation/TransactionValidatorTests.cs` + `CategoryValidatorTests.cs` exist; `Reports/PeriodReportStrategyTests.cs` + `ReportStrategyFactoryTests.cs` exist.
- [ ] `Finance.Api.IntegrationTests/TransactionsEndpointsTests.cs` + `CategoriesEndpointsTests.cs` + `ReportsEndpointTests.cs` + `ApiTestFixture.cs` exist.
- [ ] No `FluentAssertions`, `Shouldly`, or `AwesomeAssertions` references anywhere in any csproj.
- [ ] `dotnet test` runs to completion in under 15 seconds (spec SC-006).
- [ ] OpenAPI document at `/openapi/v1.json` lists `/api/transactions`, `/api/categories`, `/api/reports` (Development only) and **not** `/WeatherForecast`.
- [ ] OpenAPI / Scalar endpoints are still gated by `app.Environment.IsDevelopment()`.

## What to do next (after this feature merges)

- **JSON / CSV export feature** — add `ITransactionExportService` + per-format formatters in `Finance.Business/Exports/`, plus an `ExportController` in `Finance.Api`. Content-negotiation integration tests in `Finance.Api.IntegrationTests`.
- **CI workflow** — `.github/workflows/ci.yml` running `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` on Ubuntu.
- **IsoWeek report strategy** — register `IsoWeekReportStrategy`; populate `IsoWeekReportData`; remove the "not yet available" branch from the controller's error message. The factory and the controller need no other changes.
- **MCP context / replay layer** — sensitive-text pruning and replay snapshots over the transaction store.
