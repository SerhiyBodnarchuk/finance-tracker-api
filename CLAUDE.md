# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project at a glance

A single-user .NET Web API personal finance tracker, built as a one-week AI-assisted-development pet project. Tracks transactions and categories, generates period reports with category breakdowns, and exports JSON/CSV. Storage is seeded in-memory only — there is intentionally no database, no auth, no UI, and no multi-user support. A later milestone layers an MCP-style context/replay workflow on top of the API.

`README.md` is the canonical product/architecture spec. The two files under `ai-artifacts/Specifications/` are the per-feature contracts the AI is meant to implement (`in-memory-repository-spec.md`, `period-report-strategy-spec.md`) — they pin down field names, validation rules, seed data, and required test cases.

> **Note (2026-05-19)**: those two ai-artifacts specs predate the current design and are partially **out of date**. The newer spec at `specs/001-domain-entities-dtos/spec.md` (plus this file and `README.md`) is the authoritative source for:
>  - identifier type (`int`, not `Guid`)
>  - transaction date precision (full `DateTime`, not `DateOnly`)
>  - multi-category transactions (`Transaction.CategoryIds` is a non-empty `IReadOnlyList<int>`, not a single `CategoryId`)
>  - `ReportType` enum (`Period`, `IsoWeek`) replacing the open-string `type` field
>  - report request envelope: `{ type, data }` with a typed per-`ReportType` `data` payload (`PeriodReportData { start, end }`, `IsoWeekReportData { week }`) — `parameters`, `from`/`to` are renamed
>  - `ReportResult` is an aggregated summary: `type` + `period` (string descriptor) + `incomeTotal` + `expenseTotal` + `netTotal` + a `categoryBreakdown` collection. The underlying transactions are **not** returned. `CategoryBreakdownItem` has just `category` (name) + `total` (signed decimal — sign carries direction); no transaction count, no direction field. Multi-category transactions contribute their full signed amount to *each* attached category's breakdown line. No `currency` field. (Earlier revisions of this note said the result was just a list of transactions or earlier still that it carried the original totals/breakdown plus a transactions array — the current shape is neither.)
>  - DTO placement (Business layer, not API)
>  - ad-hoc reports (never persisted)
>
> Treat the ai-artifacts specs as background context but defer to the newer spec wherever they conflict, until they are reconciled when their corresponding features are scheduled.

## Current state vs. spec — important

The repository is at the very start of implementation. Most things described in README.md don't exist yet:

- `src/backend/FinanceTracker/Finance.Api/Controllers/` contains only the default `WeatherForecastController.cs` scaffold — no `TransactionsController`, `CategoriesController`, `ReportsController`, or `ExportController` yet.
- `Finance.Business` and `Finance.Data` projects exist and are wired with project references, but contain **no source files** — no domain models, repositories, strategies, or factory.
- Four test projects exist under `src/backend/FinanceTracker/tests/` (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, `Finance.Api.IntegrationTests`) as bare xUnit v3 scaffolds (`xunit.v3` 3.2.2, `OutputType=Exe`). They have **no `ProjectReference`s** yet — the in-progress `001-domain-entities-dtos` feature adds those to the first two and writes their test files. Assertions use the built-in `Xunit.Assert` API; **FluentAssertions is NOT used** in this project (constitution drift; see [specs/001-domain-entities-dtos/plan.md](specs/001-domain-entities-dtos/plan.md)).
- `.github/workflows/` is empty — no `ci.yml` yet.
- `ai-artifacts/agent_log.txt`, `context_schema.md`, and `example_context_snapshot.json` are empty placeholders.

Treat the README and specs as a design brief, not a description of running code.

## Layout reference

Real on-disk layout (README is now aligned with this):

- Solution file: `src/backend/FinanceTracker/FinanceTracker.slnx`.
- Projects: `Finance.Api`, `Finance.Business`, `Finance.Data` under `src/backend/FinanceTracker/`. A `Finance.Tests` project is planned but not yet created.
- AI artifacts: `ai-artifacts/` (specs under `ai-artifacts/Specifications/`).
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
- `Finance.Business` owns **all DTOs** (records) — both the HTTP request/response shapes (`TransactionCreateRequest`/`TransactionResponse`, `CategoryCreateRequest`/`CategoryResponse`) and the report contracts (`ReportRequest`, `PeriodReportData`, `IsoWeekReportData`, `ReportResult`, `CategoryBreakdownItem`) — plus the `ReportType` enum, the trivial 1:1 mappers between domain entities and DTOs (`TransactionMapper`, `CategoryMapper`), all report aggregation logic (filtering, summing totals, building the per-category breakdown, sorting it), the report **factory + strategy** pattern, validation, and MCP abstractions later. It must not reference Api. **The API layer never sees a domain entity** — mapping from `Transaction`/`Category` to `TransactionResponse`/`CategoryResponse` happens here, before the result crosses the layer boundary.
- `Finance.Api` owns controllers/minimal-API endpoints, model binding (reusing the Business-layer DTOs directly), status-code mapping, OpenAPI/Scalar setup, and DI composition. **No aggregation logic in controllers** — controllers receive a request, ask the factory for a strategy, run it, return the result. API-specific models are added here only when there is a concrete need beyond the Business-layer DTOs (none for the MVP).

Repositories are registered as **singletons** so in-memory data persists across requests for the app lifetime. Identifiers are `int` (not `Guid`); deterministic seed IDs (e.g., 1, 2, 3…) must be used for seed data so tests can assert on them.

## Report system — the central design pattern

Reports go through a single endpoint `POST /api/reports` that takes a two-field envelope: `type` (a `ReportType` enum value) and `data` (a payload whose shape is determined by `type`).

```json
{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }
```

A `ReportStrategyFactory` resolves the `ReportType` enum value to an `IReportStrategy`. Each strategy reads its strongly-typed `data` payload (`PeriodReportData`, `IsoWeekReportData`, …), validates, resolves the payload to a `[start, end]` date range, filters the transactions, computes the totals and per-category breakdown, and returns a `ReportResult`. Only `PeriodReportStrategy` is in scope for the MVP — do **not** scaffold `IsoWeekReportStrategy` or `MonthReportStrategy` yet. **Reports are ad-hoc** — every response is computed fresh from current entity state on every request and is never persisted, cached, or otherwise stored.

**`ReportResult` is an aggregated summary, not a transaction list.** It carries `type`, `period` (string descriptor), `incomeTotal`, `expenseTotal`, `netTotal`, and `categoryBreakdown` — and that's all. The contributing transactions are not in the response; a consumer that needs them queries `/api/transactions` with a date filter separately.

Aggregation rules that are easy to get wrong (full list in `ai-artifacts/Specifications/period-report-strategy-spec.md` — but see the note at the top of this file about what is now out of date there):

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

Tests use **xUnit v3** (`xunit.v3`) with the built-in `Xunit.Assert` API for assertions. **No FluentAssertions** — the ai-artifacts/Specifications/*.md files still reference FluentAssertions, but the project moved off it (see [specs/001-domain-entities-dtos/plan.md](specs/001-domain-entities-dtos/plan.md) drift #6). Use `Assert.Equal`, `Assert.True`, `Assert.Throws<T>`, `Assert.Collection`, etc. CI (planned at `.github/workflows/ci.yml`) runs `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` with no external dependencies.

## AI-assisted development log

Every meaningful AI interaction (accepted or rejected) is logged to `ai-artifacts/agent_log.txt` with timestamp, model/tool, prompt, suggestion, decision, and reason. This log is a graded deliverable — when you make a non-trivial AI-driven change, append an entry rather than letting it go undocumented.

## Out of scope — do not add

SQL Server, EF Core, Docker, LocalDB, database migrations, authentication, authorization, multi-user support, frontend UI, bank/payment integrations. The README's "Out of scope" list is binding for this MVP.

<!-- SPECKIT START -->
**Current feature**: `002-in-memory-repositories` ([spec.md](specs/002-in-memory-repositories/spec.md), [plan.md](specs/002-in-memory-repositories/plan.md))

For technologies in use, project structure, shell commands, and Phase 0/1 design decisions, read [specs/002-in-memory-repositories/plan.md](specs/002-in-memory-repositories/plan.md) first. Supporting artifacts: [research.md](specs/002-in-memory-repositories/research.md), [data-model.md](specs/002-in-memory-repositories/data-model.md), [contracts/repository-contracts.md](specs/002-in-memory-repositories/contracts/repository-contracts.md), [quickstart.md](specs/002-in-memory-repositories/quickstart.md).

> The plan's Constitution Check passes against [.specify/memory/constitution.md](.specify/memory/constitution.md) **v2.0.1** (ratified 2026-05-18, last amended 2026-05-21) — all five principles PASS with no drifts and an empty Complexity Tracking section. Repositories live in `Finance.Data/Repositories/`, are registered as singletons in `Program.cs`, use deterministic literal seed ids (categories 1–5, transactions 1–5) with `_nextId = 6`, and enforce only the case-insensitive category-name uniqueness invariant — all other validation (`Amount > 0`, referential integrity, type compatibility) is deferred to the business layer in the next feature. `/speckit-tasks` is unblocked.

> The previous feature `001-domain-entities-dtos` ([spec.md](specs/001-domain-entities-dtos/spec.md), [plan.md](specs/001-domain-entities-dtos/plan.md)) shipped the `Transaction` / `Category` records, the three enums (`TransactionType`, `CategoryType`, `ReportType`), the request/response and report DTOs in `Finance.Business`, the mappers, and the unit-test coverage for all of those. This feature consumes those entities and does not modify them.
<!-- SPECKIT END -->
