# finance-tracker-api

Single-user .NET personal finance API with SQL Server persistence, period-based reports, CI tests, and MCP-style context replay for AI-assisted development.

## Overview

`finance-tracker-api` is an API-only personal finance manager built as a pet project for practicing AI-assisted development with Cursor / Claude Code.

The application helps a single local user track income and expenses, assign categories, generate summaries for any time period, view category breakdowns, and export data. A second part of the project extends the API with an MCP-style context workflow to make AI-assisted transaction categorization and code changes more deterministic, testable, and replayable.

## Goals

The project has two main goals:

1. Build a small working MVP using an AI coding assistant.
2. Extend the MVP with MCP-style context handling to compare the original AI workflow with a structured context-based workflow.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Web API |
| Language | C# |
| ORM | Entity Framework Core |
| Database | SQL Server / LocalDB |
| Testing | xUnit + FluentAssertions |
| CI | GitHub Actions |

For automated tests the project uses an in-memory or lightweight test database provider to avoid requiring a local SQL Server instance in CI.

## Scope

This is a **single-user personal finance API**.

**In scope:**
- Create and list transactions
- Create and list categories
- Assign categories to transactions
- Generate weekly/monthly income and expense summaries
- Generate category breakdowns
- Export data as JSON or CSV
- Add MCP-style context snapshots for deterministic transaction categorization
- Log AI-assisted development decisions

## Core Domain

| Entity | Description |
|---|---|
| `Transaction` | A single income or expense entry |
| `Category` | Groups transactions (e.g. Groceries, Transport, Salary) |
| `Report` | Aggregated income/expense totals for a given period |
| `CategoryBreakdown` | Per-category totals within a time range |
| `FinanceContextSnapshot` | MCP-style snapshot used for AI-assisted categorization |

Example categories: Groceries, Transport, Entertainment, Salary, Utilities, etc.

## API Endpoints

```http
GET    /api/transactions
POST   /api/transactions
GET    /api/transactions/{id}
DELETE /api/transactions/{id}

GET    /api/categories
POST   /api/categories

GET    /api/reports/weekly?weekStart=2026-05-04
GET    /api/reports/categories?from=2026-05-01&to=2026-05-31

GET    /api/export/json
GET    /api/export/csv
```

MCP-style endpoints:

```http
POST /api/mcp/context
POST /api/mcp/actions
POST /api/mcp/results
POST /api/mcp/confirm
POST /api/mcp/rollback
```

## MCP-Style Workflow

The MCP-style extension provides structured context to an AI agent, replacing one-shot plain prompts with a deterministic, replayable loop.

**Context may include:**
- Finance profile (currency, timezone)
- Category mappings
- Pending transactions
- Previous categorization decisions
- Verification rules
- Redacted fields

## Repository Structure

```
finance-tracker-api/
  src/
    Finance.Api/
  tests/
    Finance.Api.Tests/
  artifacts/
    context_schema.md
    example_context_snapshot.json
    agent_log.txt
  .github/
    workflows/
      ci.yml
  README.md
```

## Artifacts

The `artifacts/` folder contains documentation required for the assignment.

| File | Purpose |
|---|---|
| `context_schema.md` | MCP context schema — field purposes, TTL, redaction rules, pruning rules, verification rules |
| `example_context_snapshot.json` | Safe example MCP context snapshot with sample category mappings and pending transactions |
| `agent_log.txt` | AI-assisted development log — accepted/rejected suggestions with reasoning |

## Running Locally

**Prerequisites:**
- .NET SDK
- SQL Server LocalDB or another local SQL Server instance

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Finance.Api
```

The API exposes Swagger in development mode:

```
https://localhost:5001/swagger
```

## Database

The application uses SQL Server for local development.

Example connection string for SQL Server LocalDB:

```
Server=(localdb)\MSSQLLocalDB;Database=FinanceTrackerDb;Trusted_Connection=True;MultipleActiveResultSets=true
```

Configure the connection string via `appsettings.Development.json`, user secrets, or environment variables. Do not commit real secrets or production connection strings.

## Testing

The test suite covers:

- Transaction creation and validation
- Category creation and validation
- Weekly and period aggregation logic (including boundary edge cases)
- Category breakdown logic
- Export logic (JSON and CSV)
- MCP context serialization
- MCP replay consistency
- Sensitive-field redaction

```bash
dotnet test
```

## CI

GitHub Actions runs on every push and pull request:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

The CI pipeline does not require real secrets or a live database.

## AI-Assisted Development Log

Every meaningful AI interaction is logged in `artifacts/agent_log.txt`.

Each entry includes:

| Field | Description |
|---|---|
| Timestamp | When the interaction occurred |
| Model / tool | e.g. Claude Sonnet 4.5, Cursor Agent |
| Prompt | What was asked |
| AI suggestion | What the AI proposed |
| Decision | Accepted or rejected |
| Reason | Why |

The goal is not to accept all AI suggestions, but to evaluate them critically and document the reasoning.

## Security and Privacy

This project must not store or expose:
- Bank account numbers or card numbers
- Real personal identifiers
- API keys, passwords, or access tokens
- Production secrets

Transaction descriptions may contain sensitive information. MCP context snapshots support pruning and redaction to prevent accidental leakage.
