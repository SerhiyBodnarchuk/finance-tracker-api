# Implementation Plan: Transaction Export (JSON / CSV)

**Branch**: `006-transaction-export` | **Date**: 2026-05-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-transaction-export/spec.md`

## Summary

Add `GET /api/transactions/export` to `TransactionsController`. The endpoint reads the `Accept` header (using ASP.NET Core's typed-header parser for correct `q=`-value ordering), returns JSON for `application/json` / `*/*`, returns RFC 4180 CSV for `text/csv`, and returns `406` for any other media type. CSV generation lives in a new static `TransactionCsvFormatter` class in `Finance.Business/Export/` so it can be unit-tested independently of the HTTP stack. No new DTOs, entities, or repositories are needed — the feature reuses `ITransactionService.GetAll()` and the existing `TransactionResponse` DTO.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core Web API; `Microsoft.AspNetCore.Http.Headers` for `GetTypedHeaders().Accept` (already in-framework — no new NuGet package)

**Storage**: Existing singleton in-memory repositories — no changes

**Testing**: xUnit v3 + Moq 4.20.x (unit); `Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory<Program>` (integration)

**Target Platform**: ASP.NET Core Web API — `http://localhost:5182` / `https://localhost:7235`

**Project Type**: REST API endpoint addition

**Performance Goals**: N/A — single-user, in-memory, no SLA

**Constraints**: No auth, no pagination, no filtering, no new NuGet packages

**Scale/Scope**: Full transaction dataset (all seeded + runtime-created transactions); two supported output formats

## Constitution Check

| Gate (Principle) | Status | Notes |
|------------------|--------|-------|
| I — Dependency direction `Api → Business → Data` | ✅ PASS | Controller calls `ITransactionService`; `TransactionCsvFormatter` in `Finance.Business`; no reverse references |
| I — API never sees a domain entity | ✅ PASS | `Export` action receives `IReadOnlyCollection<TransactionResponse>` from the service |
| I — No aggregation / storage access in controller | ✅ PASS | Controller dispatches to service + formatter; zero data logic inline |
| II — Report factory / strategy pattern | ✅ N/A | No new report type |
| III — Singleton repos / deterministic IDs | ✅ N/A | No new repos or seed data |
| IV — Required test coverage | ✅ PLAN | `Finance.Business.UnitTests/Export/TransactionCsvFormatterTests` + `Finance.Api.IntegrationTests/Controllers/TransactionsExportTests` — covers the explicit constitution bullet ("Export logic for JSON and CSV") |
| V — No out-of-scope tech | ✅ PASS | No DB, no auth, no Docker, no new NuGet packages |

## Project Structure

### Documentation (this feature)

```text
specs/006-transaction-export/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── http-endpoint.md ← Phase 1 output
└── tasks.md             ← /speckit-tasks output (not yet created)
```

### Source Code

```text
src/backend/FinanceTracker/

Finance.Business/
└── Export/
    └── TransactionCsvFormatter.cs          ← NEW  static formatter class

Finance.Api/
└── Controllers/
    └── TransactionsController.cs           ← MODIFY  add Export action

tests/Finance.Business.UnitTests/
└── Export/
    └── TransactionCsvFormatterTests.cs     ← NEW

tests/Finance.Api.IntegrationTests/
└── Controllers/
    └── TransactionsExportTests.cs          ← NEW
```

**Structure Decision**: Single-project additions to the existing three-layer solution. No new projects, no new packages, no new DI registrations (formatter is static, no interface needed).

## Implementation Notes

### `TransactionCsvFormatter` (Finance.Business/Export/)

Static class; no interface; no DI registration needed.

```
public static string Format(IEnumerable<TransactionResponse> transactions)
```

- Emits header row: `Id,Description,Amount,Type,Timestamp,CategoryIds`
- Per row: `Id` (int), `Description` (RFC 4180 quoted if needed), `Amount` (invariant-culture decimal), `Type` (enum name), `Timestamp` (`yyyy-MM-ddTHH:mm:ss`), `CategoryIds` (semicolon-delimited integer list, RFC 4180 quoted if needed — in practice integer IDs never need quoting)
- RFC 4180 quoting rule: wrap in `"..."` and double internal `"` when field contains `,`, `"`, `\r`, or `\n`

### `TransactionsController.Export` action (Finance.Api/Controllers/)

```
[HttpGet("export")]
public IActionResult Export()
```

Logic:
1. Call `Request.GetTypedHeaders().Accept` → `IList<MediaTypeHeaderValue>` already sorted by quality descending.
2. If list is empty → serve JSON (default).
3. Iterate in quality order; first match wins:
   - `application/json` or `*/*` → JSON response
   - `text/csv` → CSV response
4. If no match found → `StatusCode(406)` (empty body).

JSON response: `Ok(transactions.GetAll())` + `Content-Disposition: attachment; filename=transactions.json`

CSV response: `Content(TransactionCsvFormatter.Format(transactions.GetAll()), "text/csv; charset=utf-8")` + `Content-Disposition: attachment; filename=transactions.csv`

Route conflict: `[HttpGet("export")]` is a string literal; the existing `[HttpGet("{id:int}")]` uses an `int` constraint — no conflict.
