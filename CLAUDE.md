# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project at a glance

A single-user .NET Web API personal finance tracker, built as a one-week AI-assisted-development pet project. Tracks transactions and categories, generates period reports with category breakdowns, and exports JSON/CSV. Storage is seeded in-memory only — there is intentionally no database, no auth, no UI, and no multi-user support. A later milestone layers an MCP-style context/replay workflow on top of the API.

The authoritative documents are: `.specify/memory/constitution.md` (project principles and non-negotiables), this file (day-to-day operational guidance), and `README.md` (product/architecture overview). The two files under `ai-artifacts/Specifications/` are early design briefs and are intentionally not kept in sync with the running code — defer to the constitution and this file where they conflict.

## Layout reference

- Solution file: `src/backend/FinanceTracker/FinanceTracker.slnx`.
- Three production projects under `src/backend/FinanceTracker/`: `Finance.Api`, `Finance.Business`, `Finance.Data`.
- Four test projects under `src/backend/FinanceTracker/tests/`: `Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, `Finance.Api.IntegrationTests`.
- AI artifacts: `ai-artifacts/` (early specs under `ai-artifacts/Specifications/`; running log in `agent_log.txt`).
- Kestrel binding (dev): `https://localhost:7266` / `http://localhost:5235` — see `Finance.Api/Properties/launchSettings.json`.

## API docs: Scalar, not Swagger

`Finance.Api` uses `Microsoft.AspNetCore.OpenApi` (built-in `AddOpenApi()` / `MapOpenApi()`) plus `Scalar.AspNetCore` for the UI. The OpenAPI document is served at `/openapi/v1.json` and the Scalar reference UI at `/scalar/v1` (default route from `MapScalarApiReference()`), not at `/swagger`. **Both endpoints are gated behind `app.Environment.IsDevelopment()` in `Program.cs` — they are not exposed in Release builds.** Don't add Swashbuckle back without checking with the user.

## Target framework

All three projects target **`net10.0`** by design. Don't downgrade to net8/net9 without asking — net10.0 is intentional, not a typo or oversight.

## Architecture rules (enforce these)

Three-layer architecture with a strict one-way dependency direction:

```
Finance.Api → Finance.Business → Finance.Data
```

- `Finance.Data` owns domain entities (`Transaction`, `Category` — both records) and their related enums (`TransactionType`, `CategoryType` — **enums**, not standalone entities), plus repository interfaces and the seeded `InMemory*Repository` implementations. The `Transaction` record carries `IReadOnlyList<int> CategoryIds` (non-empty) — a transaction may belong to one or more categories. The Data layer must not reference Business or Api.
- `Finance.Business` owns **all DTOs** (records) — both the HTTP request/response shapes (`TransactionCreateRequest`/`TransactionResponse`, `CategoryCreateRequest`/`CategoryResponse`) and the report contracts (`ReportRequest`, `PeriodReportData`, `IsoWeekReportData`, `ReportResult`, `CategoryBreakdownItem`) — plus the `ReportType` enum, the trivial 1:1 mappers between domain entities and DTOs (`TransactionMapper`, `CategoryMapper`), the **application service layer** under `Services/` (`ICategoryService`, `ITransactionService`, `IReportService` + implementations), all report aggregation logic and the **factory + strategy** pattern under `Services/Reports/` (`ReportStrategyFactory`, every `IReportStrategy` implementation, `ReportValidationException`), the **validation result data shapes** under `Validation/` (`ValidationResult`, `ValidationError` — the shapes consumed by API-layer validators and by `ReportValidationException`), and MCP abstractions later. It must not reference Api. **The API layer never sees a domain entity** — mapping from `Transaction`/`Category` to `TransactionResponse`/`CategoryResponse` happens inside the services, before the result crosses the layer boundary.
- `Finance.Api` owns controllers/minimal-API endpoints, model binding (reusing the Business-layer DTOs directly), status-code mapping, OpenAPI/Scalar setup, the **`Infrastructure/` folder** (the `ProblemDetailsMappers` helper and the validators under `Infrastructure/Validators/`: `ITransactionValidator`/`TransactionValidator`/`ICategoryValidator`/`CategoryValidator`), and DI composition. **No aggregation or storage access in controllers** — controllers depend on `ITransactionService` / `ICategoryService` / `IReportService` (and on a validator for `POST` endpoints), and translate the service's return values to HTTP. API-specific models are added here only when there is a concrete need beyond the Business-layer DTOs (none for the MVP).

Cross-cutting access rules (constitution Principle I, v3.0.0):

- **Controllers MUST go through Business services**, not repositories. `TransactionsController` → `ITransactionService`. `CategoriesController` → `ICategoryService`. `ReportsController` → `IReportService` (which itself wraps `IReportStrategyFactory`). Direct `ITransactionRepository` / `ICategoryRepository` injection in controllers is forbidden.
- **Validators MUST go through Business services**, not repositories. `TransactionValidator` (in `Finance.Api/Infrastructure/Validators/`) depends on `ICategoryService` for the referential-integrity + type-compatibility checks. Same access rule as controllers.

Repositories are registered as **singletons** so in-memory data persists across requests for the app lifetime. Identifiers are `int` (not `Guid`); deterministic seed IDs (e.g., 1, 2, 3…) must be used for seed data so tests can assert on them. Everything else — services, every `IReportStrategy`, the `ReportStrategyFactory`, `IReportService`, and the validators — is registered as **scoped**. The repositories carry the only process-wide state; everything that depends on them is stateless and gets a fresh instance per request.

## Report system — the central design pattern

Reports go through a single endpoint `POST /api/reports` that takes a two-field envelope: `type` (a `ReportType` enum value) and `data` (a payload whose shape is determined by `type`).

```json
{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }
```

A `ReportStrategyFactory` resolves the `ReportType` enum value to an `IReportStrategy`. Each strategy reads its strongly-typed `data` payload (`PeriodReportData`, `IsoWeekReportData`, …), validates, resolves the payload to a `[start, end]` date range, filters the transactions, computes the totals and per-category breakdown, and returns a `ReportResult`. Only `PeriodReportStrategy` is in scope for the MVP — do **not** scaffold `IsoWeekReportStrategy` or `MonthReportStrategy` yet. **Reports are ad-hoc** — every response is computed fresh from current entity state on every request and is never persisted, cached, or otherwise stored.

**`ReportResult` is an aggregated summary, not a transaction list.** It carries `type`, `period` (string descriptor), `incomeTotal`, `expenseTotal`, `netTotal`, and `categoryBreakdown` — and that's all. The contributing transactions are not in the response; a consumer that needs them queries `/api/transactions` with a date filter separately.

Aggregation rules that are easy to get wrong:

- `data.start` / `data.end` (Period) are **calendar dates** (`yyyy-MM-dd`), inclusive on both ends. Transactions carry full `DateTime` timestamps (year–second precision), so the strategy treats the range as `[start 00:00:00, end 23:59:59]` and includes any transaction whose timestamp falls inside it.
- For `IsoWeek`, the strategy parses `data.week` (e.g., `"2026-W19"`) into the Monday–Sunday date range and applies the same `[from 00:00:00, to 23:59:59]` rule.
- `incomeTotal` and `expenseTotal` are **positive-or-zero** (each is just a sum of unsigned amounts). `netTotal = incomeTotal − expenseTotal` and may be negative.
- `period` is a **string descriptor** — `"2026-W20"` for `IsoWeek`, `"yyyy-MM-dd..yyyy-MM-dd"` for `Period`. Future report types document their own descriptor format.
- `CategoryBreakdownItem.total` is **signed** (positive = income side, negative = expense side). No separate direction field, no transaction count.
- **Multi-category attribution**: a transaction with N attached categories contributes its full signed amount to each of those N breakdown items. Transactions are not split or pro-rated. Consequence: the arithmetic sum of `categoryBreakdown[*].total` may exceed `netTotal` in absolute value when multi-category transactions are present — this is by design.
- `categoryBreakdown` is sorted income-side first (positive totals — zero-total `Both`-type categories sort here), then expense-side (negative totals); by category name ascending within each group.
- Missing/invalid `data` fields → `400`. Unknown report `type` value (outside the `ReportType` enum) → `400`. Period with `start > end` → `400`. Malformed `IsoWeek.week` string → `400`.

This pattern is the template for the project's reusable AI skill (`report-strategy-scaffold`), so keep the parse-typed-data → resolve-to-range → filter → aggregate → sort-breakdown shape clean and copyable when implementing it.

## Domain rules worth knowing

- Identifiers are `int` (transactions and categories). No `Guid` anywhere.
- Transaction timestamp is a full `DateTime` (year, month, day, hour, minute, second) in the user's local clock — not `DateOnly`, not a `DateTimeOffset`.
- Transaction `Amount` must be `> 0` in requests (the type encodes direction).
- A transaction may be tagged with **one or more** categories (`Transaction.CategoryIds` is a non-empty `IReadOnlyList<int>`). When there are multiple, **every** attached category must be compatible with the transaction's `TransactionType`: an `Expense` transaction may carry any combination of `Expense`/`Both` categories but no `Income`-only category, and symmetrically for `Income`.
- Duplicate category names are rejected **case-insensitively**.
- All amounts are USD by assumption; no currency field anywhere.
- DTOs and entities have identical attribute sets for the MVP, but are distinct types. Cross the boundary through `TransactionMapper` / `CategoryMapper` in the Business layer — never accept a domain entity in place of a DTO or vice versa.
- Transaction descriptions may contain sensitive text — the MCP context layer (not yet built) is responsible for redaction/pruning before any external use.

## Commands

Run from the solution directory:

```powershell
cd src/backend/FinanceTracker
dotnet restore
dotnet build
dotnet test
dotnet run --project Finance.Api
```

A single test (once a test project exists) — xUnit fully-qualified name filter:

```powershell
dotnet test --filter "FullyQualifiedName~PeriodReportStrategyTests.Excludes_transactions_outside_range"
```

Tests use **xUnit v3** (`xunit.v3`) with the built-in `Xunit.Assert` API for assertions. **No FluentAssertions** anywhere in the project. Use `Assert.Equal`, `Assert.True`, `Assert.Throws<T>`, `Assert.Collection`, etc. Unit tests use **Moq** 4.20.x to isolate the system under test from its collaborators (strict-mode mocks for both `Finance.Business.UnitTests` and `Finance.Api.UnitTests`); integration tests in `Finance.Api.IntegrationTests` use **`Microsoft.AspNetCore.Mvc.Testing`** + `WebApplicationFactory<Program>`. Both are test-only and never referenced from production code. The test folder structure mirrors production layout 1:1 (e.g. `Finance.Business/Services/CategoryService.cs` → `Finance.Business.UnitTests/Services/CategoryServiceTests.cs`). CI (planned at `.github/workflows/ci.yml`) runs `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` with no external dependencies.

## AI-assisted development log

Every meaningful AI interaction (accepted or rejected) is logged to `ai-artifacts/agent_log.txt` with timestamp, model/tool, prompt, suggestion, decision, and reason. This log is a graded deliverable — when you make a non-trivial AI-driven change, append an entry rather than letting it go undocumented.

## Out of scope — do not add

SQL Server, EF Core, Docker, LocalDB, database migrations, authentication, authorization, multi-user support, frontend UI, bank/payment integrations. The README's "Out of scope" list is binding for this MVP.

