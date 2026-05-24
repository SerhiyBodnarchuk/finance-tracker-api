# finance-tracker-api

Single-user .NET personal finance API with seeded in-memory storage, period-based reports, CI tests, and MCP-style context replay for AI-assisted development.

## Overview

`finance-tracker-api` is an API-only personal finance manager built as a pet project for practicing AI-assisted development with Cursor / Claude Code.

The application helps a single local user track income and expenses, assign categories, generate reports for requested time periods, view category breakdowns, and export report results.

The project intentionally uses seeded in-memory storage instead of a persistent database. Data is reset every time the application restarts. This keeps the MVP focused on business logic, report generation, test coverage, AI-assisted development workflow, and MCP-style context replay.

The project uses a simple three-layer architecture:

```text
API -> Business -> Data
```

A later part of the project extends the API with an MCP-style context workflow to make AI-assisted transaction categorization and code changes more deterministic, testable, and replayable.

## Goals

The project has two main goals:

1. Build a small working MVP using an AI coding assistant.
2. Extend the MVP with MCP-style context handling to compare the original AI workflow with a structured context-based workflow.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Web API |
| Runtime | .NET 10 |
| Language | C# |
| Architecture | Three-layer architecture: API, Business, Data |
| API docs | Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore |
| Storage | Seeded in-memory repositories (data resets on restart by design) |
| Testing | xUnit v3 (built-in `Xunit.Assert`; no FluentAssertions). Moq for unit-test isolation; `Microsoft.AspNetCore.Mvc.Testing` for integration tests |
| CI | GitHub Actions |

No SQL Server, EF Core, Docker, LocalDB, or database migrations are required.

## Architecture

The solution should be organized into three main layers.

```text
finance-tracker-api/
  src/
    backend/
      FinanceTracker/
        FinanceTracker.slnx
        Finance.Api/              (Controllers/, Infrastructure/Validators/, Program.cs)
        Finance.Business/         (Dtos/, Mappers/, Services/, Services/Reports/, Validation/, Enums/)
        Finance.Data/             (Models/, Repositories/)
        tests/
          Finance.Data.UnitTests/
          Finance.Business.UnitTests/      (Services/, Services/Reports/, Dtos/, Mappers/)
          Finance.Api.UnitTests/           (Controllers/, Infrastructure/Validators/)
          Finance.Api.IntegrationTests/    (WebApplicationFactory<Program>-based)
  ai-artifacts/
    Specifications/
      in-memory-repository-spec.md
      period-report-strategy-spec.md
    context_schema.md             (placeholder)
    example_context_snapshot.json (placeholder)
    agent_log.txt                 (placeholder)
  .github/
    workflows/
      ci.yml                      (planned)
  CLAUDE.md
  README.md
```

### API Layer

Project: `Finance.Api`

Responsibilities:

- HTTP controllers or minimal API endpoints
- Request/response HTTP concerns
- Model binding (reusing Business-layer DTOs directly)
- Status code mapping
- OpenAPI document + Scalar reference UI (dev-only)
- Dependency injection composition
- Validators under `Infrastructure/Validators/` that translate request DTOs into `ValidationProblemDetails`; they depend on Business-layer services (not on repositories)
- Problem-details mappers and other HTTP-shaped plumbing under `Infrastructure/`
- No business aggregation logic
- No direct in-memory data manipulation (controllers and validators MUST go through Business services)
- API-specific models are added here only when there is a concrete need beyond the Business-layer DTOs (none for the MVP)

The API layer depends on the Business layer.

### Business Layer

Project: `Finance.Business`

Responsibilities:

- Request and response DTOs (records) for transactions, categories, and reports — the API layer reuses them directly
- Mappers between Data-layer domain entities and the DTOs above (trivial 1:1 for the MVP; their purpose is the layer boundary, not data transformation)
- Application services under `Services/` (`ICategoryService`, `ITransactionService`, `IReportService`) — the layer controllers route through; services return DTOs and do the entity-to-DTO mapping internally
- Report factory + strategies under `Services/Reports/` (`ReportStrategyFactory`, each `IReportStrategy` implementation, `ReportValidationException`)
- Validation result data shapes under `Validation/` (`ValidationResult`, `ValidationError`) — consumed by validators in the API layer and by `ReportValidationException` in the Business layer
- Aggregation logic
- Export formatting contracts, if needed
- MCP workflow abstractions later

The Business layer depends on the Data layer.

### Data Layer

Project: `Finance.Data`

Responsibilities:

- Domain models
- Repository interfaces
- Seeded in-memory repository implementations
- Seed data
- Domain enums such as transaction type

The Data layer should not depend on the API or Business layers.

## Dependency Direction

Allowed dependency direction:

```text
Finance.Api -> Finance.Business -> Finance.Data
```

Avoid reverse dependencies:

```text
Finance.Data -> Finance.Business
Finance.Data -> Finance.Api
Finance.Business -> Finance.Api
```

## Scope

This is a single-user personal finance API.

### In scope

- Create and list transactions
- Create and list categories
- Assign categories to transactions
- Generate reports through a factory + strategy design
- Support an initial custom period report
- Add additional report types later, such as ISO week and month reports
- Generate category breakdowns inside reports
- Export report results as JSON or CSV using content negotiation
- Use seeded in-memory data on application startup
- Add MCP-style context snapshots for deterministic transaction categorization
- Log AI-assisted development decisions

### Out of scope

- Frontend UI
- Authentication
- Authorization
- Multi-user support
- Persistent database storage
- SQL Server setup
- EF Core migrations
- Docker
- LocalDB
- Bank integrations
- Payment integrations
- Real financial advice

## Core Domain

Domain entities and the two related enums live in the Data layer.

| Type | Kind | Description |
|---|---|---|
| `Transaction` | Entity (record) | A single income or expense entry. Carries an `int` Id, a full `DateTime` timestamp (year–second precision), description, positive `decimal` amount, a `TransactionType`, and a **non-empty `IReadOnlyList<int>` of category references** (a transaction may be tagged with one or more categories). |
| `Category` | Entity (record) | Groups transactions, for example Groceries, Transport, Salary. Carries an `int` Id, name, and a `CategoryType`. |
| `TransactionType` | Enum | `Income` or `Expense`. An attribute on `Transaction` — not a standalone entity. |
| `CategoryType` | Enum | `Income`, `Expense`, or `Both`. An attribute on `Category` — not a standalone entity. |
| `ReportType` | Enum | `Period`, `IsoWeek` (extensible later). An attribute on `ReportRequest` that discriminates the request's `data` payload. |

Identifiers are `int` (not `Guid`); all amounts are USD (no currency field is stored or returned).

**Multi-category compatibility rule** (enforced by validation, not by the contract): every category attached to a transaction must be compatible with the transaction's direction — i.e., an `Expense` transaction MAY be tagged with any combination of `Expense`/`Both` categories but MUST NOT include any `Income`-only category, and symmetrically for `Income`.

DTOs (records) and report contracts live in the Business layer. The API layer reuses them directly.

| Business Contract | Description |
|---|---|
| `TransactionCreateRequest` / `TransactionResponse` | DTOs for the transactions endpoint. `TransactionResponse` carries an inline `categories` collection (each item: `{ id, name }`). |
| `CategoryCreateRequest` / `CategoryResponse` | DTOs for the categories endpoint |
| `ReportRequest` | Envelope: `{ type: ReportType, data: <per-type payload> }` |
| `PeriodReportData` | Used when `type = Period`. Fields: `start` (date), `end` (date) — both inclusive. |
| `IsoWeekReportData` | Used when `type = IsoWeek`. Field: `week` (ISO 8601 week string, e.g. `"2026-W19"`). |
| `ReportResult` | The aggregated report response. Carries `type`, `period` (string descriptor — `"yyyy-Www"` for ISO-week, `"yyyy-MM-dd..yyyy-MM-dd"` for custom period), `incomeTotal`, `expenseTotal`, `netTotal`, and `categoryBreakdown`. Does **not** return the underlying transactions. Computed on demand; never persisted. |
| `CategoryBreakdownItem` | A row in `ReportResult.categoryBreakdown`. Two fields: `category` (name) and `total` (signed decimal — sign carries direction). No transaction count. |
| `FinanceContextSnapshot` | MCP-style snapshot used for AI-assisted categorization and replay |

Trivial 1:1 mappers between the domain entities and their DTOs (e.g., `TransactionMapper`, `CategoryMapper`) also live in the Business layer, alongside the report-strategy implementations that filter, aggregate, and build the breakdown.

Example categories:

- Groceries
- Transport
- Entertainment
- Salary
- Utilities

## Storage Model

The API uses in-memory repositories in the Data layer.

Data is seeded when the application starts and is lost when the application stops.

This is intentional because the project is a one-time assignment MVP. The focus is on:

- API behavior
- three-layer architecture
- report strategy design
- aggregation logic
- test coverage
- CI execution
- AI-assisted development documentation
- MCP-style context replay

Persistence can be added later behind repository interfaces if needed.

## API Endpoints

```http
GET    /api/transactions
POST   /api/transactions
GET    /api/transactions/{id}
DELETE /api/transactions/{id}

GET    /api/categories
GET    /api/categories/{id}
POST   /api/categories

POST   /api/reports

POST   /api/export    (planned, not yet implemented)
```

Optional MCP-style endpoints or internal services:

```http
POST /api/mcp/context
POST /api/mcp/actions
POST /api/mcp/results
POST /api/mcp/confirm
POST /api/mcp/rollback
```

The MCP implementation may be kept as an internal C# service instead of public HTTP endpoints.

## Reports

Reports are generated through a single endpoint:

```http
POST /api/reports
Content-Type: application/json
Accept: application/json
```

The request body is an envelope of exactly two fields: `type` (a `ReportType` enum value) and `data` (a payload whose shape depends on `type`). The response is an **aggregated summary** — totals plus a per-category breakdown. The underlying transactions are **not** returned; a consumer that needs them queries the transactions endpoint with a date filter separately.

Supported report types:

| Type | `data` fields | Description |
|---|---|---|
| `Period` | `start` (date), `end` (date) | Aggregates transactions whose timestamp falls in `[start, end]` (inclusive on both ends, interpreted as `start 00:00:00 .. end 23:59:59`). |
| `IsoWeek` | `week` (string, ISO 8601 — e.g., `"2026-W19"`) | Aggregates transactions whose timestamp falls in the named ISO week (Monday 00:00:00 through Sunday 23:59:59). |

Planned future report types:

| Type | `data` fields | Description |
|---|---|---|
| `Month` | `year` (int), `month` (int) | Aggregates transactions whose timestamp falls in the calendar month. |

### ISO-week report example

Request:

```json
{
  "type": "IsoWeek",
  "data": {
    "week": "2026-W20"
  }
}
```

Response:

```json
{
  "type": "IsoWeek",
  "period": "2026-W20",
  "incomeTotal": 1200.00,
  "expenseTotal": 430.50,
  "netTotal": 769.50,
  "categoryBreakdown": [
    { "category": "Salary",     "total":  1200.00 },
    { "category": "Dining out", "total":   -65.25 },
    { "category": "Groceries",  "total":  -180.25 },
    { "category": "Transport",  "total":   -65.00 },
    { "category": "Utilities",  "total":  -120.00 }
  ]
}
```

`categoryBreakdown` is ordered with income-side categories first (positive `total`), then expense-side categories (negative `total`); within each of those groups items are sorted by category name ascending. The `total` field is **signed** — the sign is the only direction indicator; there is no separate direction field and no transaction count.

### Period report example

Request:

```json
{
  "type": "Period",
  "data": {
    "start": "2026-05-01",
    "end": "2026-05-31"
  }
}
```

Response (same `ReportResult` shape; `period` is the stringified inclusive range):

```json
{
  "type": "Period",
  "period": "2026-05-01..2026-05-31",
  "incomeTotal": 1200.00,
  "expenseTotal": 121.29,
  "netTotal": 1078.71,
  "categoryBreakdown": [
    { "category": "Salary",      "total":  1200.00 },
    { "category": "Entertainment","total":   -9.99 },
    { "category": "Groceries",   "total":   -32.10 },
    { "category": "Transport",   "total":   -14.20 },
    { "category": "Utilities",   "total":   -65.00 }
  ]
}
```

`data.start` and `data.end` are calendar dates (`yyyy-MM-dd`); transactions carry full timestamps and the strategy treats the period as `[start 00:00:00, end 23:59:59]` inclusive. All amounts are USD; no `currency` field is returned.

**Multi-category attribution**: when a transaction is tagged with multiple categories, its full signed amount is added to **each** of those categories' breakdown lines (a $20 grocery run tagged "Groceries" + "Health Food" adds −$20 to both). As a result, the arithmetic sum of `categoryBreakdown[*].total` may exceed (in absolute value) `netTotal` whenever multi-category transactions are present — this is expected.

## Report Architecture

Reports use a factory + strategy design in the Business layer.

The `ReportsController` delegates to `IReportService`, which resolves the `ReportRequest.Type` enum value through `ReportStrategyFactory` to an `IReportStrategy` implementation. Each strategy owns its full pipeline:

1. Deserialize `ReportRequest.Data` (a `JsonElement`) into its own typed payload DTO.
2. Validate the payload; on failure throw `ReportValidationException` (controller maps to HTTP 400).
3. Resolve the payload to an inclusive `[start 00:00:00, end 23:59:59]` `DateTime` range.
4. Filter the transactions; compute `incomeTotal`, `expenseTotal`, `netTotal`.
5. Build the per-category breakdown with multi-category attribution and the income-first / expense-second / alphabetical-within sort.
6. Return a `ReportResult`.

Currently implemented strategies:

- `PeriodReportStrategy` (`ReportType.Period`)
- `IsoWeekReportStrategy` (`ReportType.IsoWeek`)

Planned strategies (the enum values are not yet present; both the enum entry and the strategy need to be added):

- `MonthReportStrategy` (`ReportType.Month`)

Adding a new strategy means: define its `data` payload DTO under `Finance.Business/Dtos/Reports/`, implement `IReportStrategy` under `Finance.Business/Services/Reports/`, and register it as a DI singleton in `Program.cs`. The factory picks it up automatically because it's built from the DI-resolved `IEnumerable<IReportStrategy>`.

The reusable AI skill for this project should focus on generating a new report strategy, parameter parsing, factory registration, and unit tests for a new report type.

## Export

Planned (not yet implemented).

Reports will be exported through a single endpoint:

```http
POST /api/export
```

The request body will carry the same `ReportRequest` envelope used by `/api/reports` (`{ type, data }`); the response format will be selected through the `Accept` header (`application/json` or `text/csv`). Exports will return the generated `ReportResult`, not the raw repository state.

## MCP-Style Workflow

The MCP-style extension provides structured context to an AI agent, replacing one-shot plain prompts with a deterministic, replayable loop.

Context may include:

- Finance profile, such as currency and timezone
- Category mappings
- Pending transactions
- Previous categorization decisions
- Verification rules
- Redacted fields

Normal flow:

```text
sendContext -> requestAction -> receiveResult -> verify -> confirm
```

Failure/refinement flow:

```text
sendContext -> requestAction -> receiveResult -> verify failed -> refine -> confirm or rollback
```

The purpose is to prove that structured context makes AI-assisted work more reproducible than plain prompts.

## Artifacts

The `ai-artifacts/` folder contains documentation required for the assignment.

| File | Purpose |
|---|---|
| `agent_log.txt` | AI-assisted development log: accepted/rejected suggestions with reasoning |
| `context_schema.md` | MCP context schema: field purposes, TTL, redaction rules, pruning rules, verification rules (placeholder for the future MCP milestone) |
| `example_context_snapshot.json` | Safe example MCP context snapshot (placeholder for the future MCP milestone) |
| `Specifications/in-memory-repository-spec.md`, `Specifications/period-report-strategy-spec.md` | Early design briefs; defer to the constitution and CLAUDE.md where they conflict |

## Running Locally

Prerequisites:

- .NET SDK

No database setup is required.

```powershell
cd src/backend/FinanceTracker
dotnet restore
dotnet build
dotnet test
dotnet run --project Finance.Api
```

The API exposes its OpenAPI document and the Scalar reference UI in Development mode only:

```text
https://localhost:7235/openapi/v1.json   (OpenAPI document)
https://localhost:7235/scalar/v1         (Scalar UI)
```

The HTTP binding (`http://localhost:5182`) is also available; see `Finance.Api/Properties/launchSettings.json` for current ports.

Data is seeded on startup. Restarting the application resets the data.

## Testing

The test suite covers:

- Transaction creation and validation
- Category creation and validation
- Repository behavior
- Report strategy selection
- Period report generation
- Boundary edge cases for report periods
- Category breakdown logic
- Export logic for JSON and CSV
- MCP context serialization
- MCP replay consistency
- Sensitive-field redaction

Run tests:

```bash
dotnet test
```

## CI

GitHub Actions runs on every pull request to `main` or `development` (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml)). Each PR runs on `ubuntu-latest` against .NET 10:

```bash
dotnet restore FinanceTracker.slnx
dotnet build  FinanceTracker.slnx --no-restore --configuration Release --verbosity minimal
dotnet test   FinanceTracker.slnx --no-build  --configuration Release
```

NuGet packages are cached at `~/.nuget/packages` keyed on the hash of all `*.csproj` files; concurrent runs on the same ref cancel earlier ones. The CI pipeline does not require real secrets, Docker, SQL Server, LocalDB, or any external database.

## AI-Assisted Development Log

Every meaningful AI interaction is logged in:

```text
ai-artifacts/agent_log.txt
```

Each entry includes:

| Field | Description |
|---|---|
| Timestamp | When the interaction occurred |
| Model / tool | Example: Claude Sonnet, Cursor Agent |
| Prompt | What was asked |
| AI suggestion | What the AI proposed |
| Decision | Accepted or rejected |
| Reason | Why the suggestion was accepted or rejected |

The goal is not to accept all AI suggestions, but to evaluate them critically and document the reasoning.

## Suggested AI Skill

The reusable skill for this project should be:

```text
report-strategy-scaffold
```

Purpose:

```text
Given a report type, request parameters, and expected output shape, generate a C# report strategy, parameter parser, factory registration, and xUnit tests for valid input, invalid input, and aggregation edge cases.
```

Example future use:

```text
Create a report strategy for report type "isoWeek".

Input parameters:
- year: int
- week: int

Rules:
- Use ISO 8601 week boundaries.
- Include transactions from Monday through Sunday.
- Return income total, expense total, net total, category breakdown, and matching transactions.

Generate:
- IsoWeekReportStrategy
- parameter parser
- factory registration
- xUnit tests for valid week, invalid week, and boundary dates
```

## Security and Privacy

This project must not store or expose:

- Bank account numbers
- Card numbers
- Real personal identifiers
- API keys
- Passwords
- Access tokens
- Production secrets

Transaction descriptions may contain sensitive information. MCP context snapshots support pruning and redaction to prevent accidental leakage.

## Project Status

MVP build-out is well underway.

Milestones:

1. ✅ Create three projects: API, Business, and Data.
2. ✅ Add in-memory repositories with seeded transactions and categories in the Data layer.
3. ✅ Add transaction and category endpoints in the API layer.
4. ✅ Add report factory and initial period report strategy in the Business layer.
5. ⬜ Add JSON and CSV export endpoint using content negotiation.
6. ✅ Add unit and integration tests (xUnit v3 + Moq for unit isolation; `Microsoft.AspNetCore.Mvc.Testing` for end-to-end HTTP coverage).
7. ✅ Add GitHub Actions CI.
8. ⬜ Add MCP-style context snapshot and replay flow.
9. ⬜ Complete assignment artifacts and demo recording.