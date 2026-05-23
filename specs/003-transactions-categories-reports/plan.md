# Implementation Plan: Transactions / Categories Endpoints with Business Validation and Period Reports

**Branch**: `003-transactions-categories-reports` | **Date**: 2026-05-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-transactions-categories-reports/spec.md`

## Summary

Deliver the **HTTP surface and business logic** that turns the MVP into a real API: three controllers (`TransactionsController`, `CategoriesController`, `ReportsController`) in `Finance.Api`, three business-layer services (`TransactionValidator`, `CategoryValidator`, the `IReportStrategy` / `ReportStrategyFactory` pair plus the concrete `PeriodReportStrategy`) in `Finance.Business`, plus the DI registrations that wire it all together in `Program.cs`. Every endpoint speaks DTOs only — domain entities never leave the Business layer. Validation rules deferred from feature 002 (`Amount > 0`, `Description` non-empty, non-empty `CategoryIds`, referential integrity, transaction-type ↔ category-type compatibility) land here in the Business layer. The summary-only `ReportResult` shape from constitution Principle II is implemented end-to-end including the multi-category attribution rule and the income-first / expense-second / alphabetical-within sort. JSON / CSV export is **explicitly out of scope** per the user's `/speckit-specify` direction. The default `WeatherForecastController.cs` scaffold is removed.

Technical approach: validation uses a return-based `ValidationResult` value type (no exceptions for input-shape problems). Controllers translate `ValidationResult` to `BadRequest(ProblemDetails)`, translate the `InvalidOperationException` that `InMemoryCategoryRepository.Add` already throws for duplicate names into `409 Conflict`, and translate `null` from `GetById` / `false` from `Delete` into `404 Not Found`. The report strategy gets the `ReportRequest` envelope and deserializes its own typed payload (`PeriodReportData`) from the `JsonElement Data` field — exactly the lazy-typed pattern the constitution describes. The factory holds a small `Dictionary<ReportType, IReportStrategy>` populated from DI-resolved strategies; an unregistered enum value (`IsoWeek`) makes the factory return `null`, which the controller maps to 400 with a message that names the unsupported type. Multi-category attribution is implemented by iterating each in-window transaction once and accumulating its signed amount into every attached category's running total; the breakdown is then filtered to non-zero / non-empty groups, sorted, and emitted. Integration tests live in the existing-but-empty `Finance.Api.IntegrationTests` project and use `WebApplicationFactory<Program>` against an in-memory test server — the one new NuGet package this feature pulls in.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`). All projects already have nullable + implicit usings enabled.

**Primary Dependencies**:

- Existing: `Microsoft.AspNetCore.OpenApi` 10.0.8, `Scalar.AspNetCore` 2.14.14 in `Finance.Api`; `xunit.v3` 3.2.2 in all four test projects; `Microsoft.Extensions.DependencyInjection` 10.0.0 in `Finance.Data.UnitTests` (added in feature 002).
- **New package**: `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 added to `Finance.Api.IntegrationTests/Finance.Api.IntegrationTests.csproj`. This is the canonical ASP.NET Core in-memory test server used by every integration test in this feature; there is no lighter alternative that exercises the real request pipeline (model binding, content negotiation, routing). The package is official, in the shared framework family, and ~120 KB. The plan deliberately calls it out because the previous feature claimed "no new NuGet packages added" and this one does add one.
- **No other new packages**. The validators and the report strategy use BCL types only (`StringComparer.OrdinalIgnoreCase`, `System.Text.Json`, LINQ).

**Storage**: In-memory via the singleton repositories from feature 002. This feature adds zero storage changes — it consumes `ITransactionRepository` and `ICategoryRepository` and never bypasses them.

**Testing**: **xUnit v3** (`xunit.v3` 3.2.2) with the **built-in `Xunit.Assert` API**. **No FluentAssertions** (constitution Principle IV). Coverage in this feature spans three of the four test projects:

- `Finance.Business.UnitTests/` — adds tests for `TransactionValidator`, `CategoryValidator`, `ReportStrategyFactory`, `PeriodReportStrategy` (~20 new tests).
- `Finance.Api.UnitTests/` — remains empty in this feature. The controllers' behavior is HTTP-shaped and is more usefully tested at the integration level; unit-testing them would just exercise the framework's binders.
- `Finance.Api.IntegrationTests/` — adds end-to-end tests against `WebApplicationFactory<Program>` covering every endpoint (~10–12 new tests). The project's existing `<ProjectReference>` to `Finance.Api` is already on disk; this feature only adds the `Microsoft.AspNetCore.Mvc.Testing` package reference and the test files.
- `Finance.Data.UnitTests/` — unchanged.

**Target Platform**: Cross-platform .NET 10 runtime. Development host is Windows 11 + PowerShell; CI runs (when `.github/workflows/ci.yml` lands, still out of scope) will use the GitHub Ubuntu runner. `WebApplicationFactory` works identically on both.

**Project Type**: ASP.NET Core Web API. This feature touches **all three** production projects — `Finance.Business` (new validation + report services), `Finance.Api` (new controllers + DI wiring + scaffold removal), and indirectly `Finance.Data` (no source changes, but consumed by the new business services). Dependency direction `Api → Business → Data` preserved.

**Performance Goals**: Demo-grade. The seed set is 5 transactions / 5 categories; runtime additions in a demo session top out at a few dozen. Every read operation (list, get-by-id, period report over the entire window) MUST return under 500 ms p99 on a developer laptop. No specific budget needed for writes — they are O(n) over the in-memory list per validation rule and are not the bottleneck.

**Constraints**:

- Controllers MUST NOT contain aggregation logic or validation rules (constitution Principle I + Principle II). They MAY do status-code translation, problem-details composition, and `Location` header construction.
- Validation lives in the Business layer as **stateless services**, registered as singletons (the validators have no per-request mutable state; their only dependency is `ICategoryRepository` for the referential-integrity / type-compatibility checks).
- The report strategy MUST own its full parse → validate → aggregate → return pipeline. The controller MUST NOT inspect the `Data` payload, MUST NOT branch on `Type`, and MUST NOT perform aggregation.
- `ReportResult` MUST have exactly six fields (`type`, `period`, `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown`). No `transactions` array, no `currency` field. Tests pin this down.
- Multi-category attribution: a transaction with N categories contributes its full signed amount to each of the N breakdown items (constitution Principle II). The arithmetic sum of `categoryBreakdown[*].total` may exceed `netTotal` in absolute value — by design.
- OpenAPI / Scalar endpoints stay gated behind `app.Environment.IsDevelopment()` in `Program.cs`. This feature documents the new endpoints in the OpenAPI document but does not change the gating.
- No update operations are added in this feature (no `PUT` / `PATCH` on transactions or categories). Spec FRs only require create + read + delete on transactions; create + read on categories.
- No new third-party packages beyond `Microsoft.AspNetCore.Mvc.Testing`.

**Scale/Scope**:

- ~3 validator-ish classes (~150 lines) + 3 report-system classes (~200 lines) in `Finance.Business`.
- ~3 controllers (~250 lines) in `Finance.Api` + ~10 lines added to `Program.cs` for DI.
- ~30 new tests distributed across `Finance.Business.UnitTests/` and `Finance.Api.IntegrationTests/`.
- Net source: ~600 lines production + ~700 lines tests.
- The default `WeatherForecastController.cs` and `WeatherForecast.cs` files are deleted.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Result of this gate (pre-Phase-0)**: ✅ **All five principles PASS against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.1.** No drifts. No Complexity Tracking entries required.

Per-principle check:

| Principle | Status | Notes |
|---|---|---|
| I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) | ✅ **PASS** | Validators + report strategy + factory all live in `Finance.Business`. Controllers in `Finance.Api` delegate to those services and never touch repositories directly. Dependency direction `Api → Business → Data` preserved; no reverse refs. The API layer never serializes a domain entity — mapping from `Transaction`/`Category` to `TransactionResponse`/`CategoryResponse` happens via the existing Business-layer mappers from feature 001. |
| II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) | ✅ **PASS** | Single endpoint `POST /api/reports`. Typed envelope `{ ReportType, JsonElement Data }`. `ReportStrategyFactory` resolves the enum value to an `IReportStrategy`; unknown enum value → 400 at the factory (controller maps to HTTP). `PeriodReportStrategy` owns parse → validate → aggregate → return; the controller does none of that. `ReportResult` is exactly the six-field summary envelope. Multi-category attribution implemented per the rule (full signed amount to each attached category). Sort order income-first / expense-second / alphabetical-within. Reports never cached, never persisted. Only `PeriodReportStrategy` scaffolded; `IsoWeek` returns 400 from the factory. HTTP 400 mapping for missing/invalid `data`, `start > end`, unknown enum value. |
| III. Seeded In-Memory Determinism (NON-NEGOTIABLE) | ✅ **PASS** | This feature does not touch the repositories. The `Finance.Data` singleton registrations from feature 002 remain in place. Identifiers stay `int`; multi-category transactions still carried as `IReadOnlyList<int> CategoryIds`; full `DateTime` timestamps; no `currency` field anywhere. The new business-layer validators **enforce** the type-compatibility rule that the constitution Principle III bullet says is a "Business-layer concern" — exactly the layer this feature places it in. |
| IV. Test-First with xUnit v3 | ✅ **PASS** | New tests land in `Finance.Business.UnitTests` (validators + report) and `Finance.Api.IntegrationTests` (end-to-end HTTP). Both use xUnit v3 and the built-in `Xunit.Assert` API only. No FluentAssertions. Coverage targets the Principle IV bullets explicitly assigned to this feature: "Category creation and validation, including case-insensitive duplicate rejection" (the validator path; the repo path already covered in feature 002), "Transaction creation and validation, including the `Amount > 0` rule and the multi-category compatibility rule from Principle III", "Report strategy selection by `ReportType` enum value (unknown value → 400)", "Period report aggregation and the boundary edge cases / multi-category attribution invariants", "Category breakdown logic, including the income-first / expense-second / alphabetical-within-group ordering and signed `total` semantics", and "API endpoint integration coverage (status codes, request binding, serialization)". |
| V. AI-Assisted Development Transparency | ✅ **PASS** | `ai-artifacts/agent_log.txt` will be appended with the meaningful AI-driven decisions for this feature (validator shape, report strategy parse-typed-data approach, integration-tests package addition, etc.). OpenAPI / Scalar dev-only gating in `Program.cs` is preserved — no edits to the `if (app.Environment.IsDevelopment()) { ... }` block. No real PII / secrets in fixtures; all test data uses the same safe seed values introduced in feature 002. |

**Result of this gate (post-Phase-1, re-check)**: ✅ **All five principles still PASS.** The Phase 1 designs ([data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)) introduce no new package dependencies beyond the `Microsoft.AspNetCore.Mvc.Testing` already declared in the Technical Context. The validator service signatures and the strategy/factory shapes are pure Business-layer types with no reverse layer references. The HTTP contracts use only the existing DTOs from feature 001 (no new DTO types added). The OpenAPI / Scalar gating in `Program.cs` is untouched. Re-check unanimous.

## Project Structure

### Documentation (this feature)

```text
specs/003-transactions-categories-reports/
├── plan.md                            # This file
├── research.md                        # Phase 0 — design decisions
│                                      #   (validator return shape, factory lookup data structure,
│                                      #    where type-compat check lives, error envelope choice,
│                                      #    integration-test package choice, WeatherForecast scaffold,
│                                      #    DI lifetimes, IsoWeek-in-enum-but-not-in-factory handling)
├── data-model.md                      # Phase 1 — concrete validator + strategy + factory signatures,
│                                      #   controller shapes, DI table
├── contracts/                         # Phase 1 — HTTP contracts per endpoint
│   ├── http-endpoints.md              #   the umbrella table of all endpoints with status codes
│   ├── transactions-endpoints.schema.json
│   ├── categories-endpoints.schema.json
│   └── reports-endpoint.schema.json
├── quickstart.md                      # Phase 1 — build, run, manual sanity-check curl session
├── checklists/
│   └── requirements.md                # From /speckit-specify
├── spec.md                            # From /speckit-specify
└── tasks.md                           # Phase 2 (from /speckit-tasks, NOT created here)
```

### Source Code (repository root)

```text
src/backend/FinanceTracker/
├── FinanceTracker.slnx                       # Already references all 3 production + 4 test projects
├── Finance.Api/
│   ├── Finance.Api.csproj                    # Existing — no new package refs
│   ├── Program.cs                            # MODIFIED — add DI for validators + report factory + strategy;
│   │                                         #            map controllers (already present)
│   ├── Controllers/
│   │   ├── WeatherForecastController.cs      # DELETED — default scaffold removed
│   │   ├── TransactionsController.cs         # NEW — GET list, GET by id, POST create, DELETE
│   │   ├── CategoriesController.cs           # NEW — GET list, GET by id, POST create
│   │   └── ReportsController.cs              # NEW — POST /api/reports
│   └── WeatherForecast.cs                    # DELETED — default scaffold removed
├── Finance.Business/
│   ├── Finance.Business.csproj               # Existing — no new package refs
│   ├── Dtos/                                 # From feature 001 — UNTOUCHED
│   ├── Enums/                                # From feature 001 — UNTOUCHED
│   ├── Mappers/                              # From feature 001 — UNTOUCHED
│   ├── JsonSerializationOptions.cs           # From feature 001 — UNTOUCHED
│   ├── Validation/
│   │   ├── ValidationResult.cs               # NEW — { IsValid, Errors : IReadOnlyList<ValidationError> }
│   │   ├── ValidationError.cs                # NEW — { Field, Message }
│   │   ├── ITransactionValidator.cs          # NEW — ValidateForCreate(TransactionCreateRequest)
│   │   ├── ICategoryValidator.cs             # NEW — ValidateForCreate(CategoryCreateRequest)
│   │   ├── TransactionValidator.cs           # NEW — Amount > 0, Description non-empty,
│   │   │                                     #       non-empty CategoryIds, referential integrity,
│   │   │                                     #       type-compatibility
│   │   └── CategoryValidator.cs              # NEW — Name non-empty after trim
│   └── Reports/
│       ├── IReportStrategy.cs                # NEW — Generate(ReportRequest) → ReportResult
│       ├── IReportStrategyFactory.cs         # NEW — TryGet(ReportType) → IReportStrategy?
│       ├── ReportStrategyFactory.cs          # NEW — Dictionary<ReportType, IReportStrategy>
│       └── PeriodReportStrategy.cs           # NEW — parse PeriodReportData,
│                                             #       validate start/end shape + start<=end,
│                                             #       filter [start 00:00:00, end 23:59:59],
│                                             #       compute three totals,
│                                             #       multi-category attribution into breakdown,
│                                             #       sort income-first / expense-second / alphabetical
└── Finance.Data/                             # UNTOUCHED by this feature

tests/  (under src/backend/FinanceTracker/tests/)
├── Finance.Data.UnitTests/                   # UNTOUCHED by this feature
├── Finance.Business.UnitTests/
│   ├── Finance.Business.UnitTests.csproj     # Existing — no new package refs
│   ├── Validation/
│   │   ├── TransactionValidatorTests.cs      # NEW — ~8 tests (one per FR-009..FR-013 + happy path)
│   │   └── CategoryValidatorTests.cs         # NEW — ~3 tests (empty name reject, whitespace reject,
│   │                                         #                valid happy path)
│   └── Reports/
│       ├── ReportStrategyFactoryTests.cs     # NEW — ~3 tests (Period resolves; IsoWeek returns null;
│       │                                     #                Month / unknown enum value returns null)
│       └── PeriodReportStrategyTests.cs      # NEW — ~7 tests:
│                                             #         seed window happy path with the documented numbers,
│                                             #         empty window returns zeros + empty breakdown,
│                                             #         start > end rejects,
│                                             #         missing start/end rejects,
│                                             #         non-ISO date rejects,
│                                             #         multi-category attribution invariant,
│                                             #         breakdown sort order (income-first / alpha-within)
├── Finance.Api.UnitTests/                    # UNTOUCHED — no narrow API-layer unit tests in this feature
└── Finance.Api.IntegrationTests/
    ├── Finance.Api.IntegrationTests.csproj   # MODIFIED — add Microsoft.AspNetCore.Mvc.Testing 10.0.0,
    │                                         #            add <ProjectReference> to Finance.Api
    ├── ApiTestFixture.cs                     # NEW — shared WebApplicationFactory<Program> wrapper
    ├── TransactionsEndpointsTests.cs         # NEW — ~5 tests (GET list, GET by id 200/404,
    │                                         #                POST 201 with Location, DELETE 204/404)
    ├── CategoriesEndpointsTests.cs           # NEW — ~3 tests (GET list, POST 201, dup-name 409)
    └── ReportsEndpointTests.cs               # NEW — ~5 tests:
                                              #         seed window happy path returns documented numbers,
                                              #         IsoWeek returns 400,
                                              #         Month / unknown enum returns 400,
                                              #         start > end returns 400,
                                              #         two identical calls return byte-identical bodies (SC-004)

.github/
└── workflows/                                # UNTOUCHED — ci.yml is still a later feature
```

**Structure Decision**: This feature populates the `Validation/` and `Reports/` subfolders inside `Finance.Business/` (both new), the three controllers inside the existing `Finance.Api/Controllers/` folder, and the `Validation/` + `Reports/` test subfolders + new files in `Finance.Business.UnitTests`. It adds the `Microsoft.AspNetCore.Mvc.Testing` package and a project reference to `Finance.Api.IntegrationTests` (the latter is documented but not yet present), then populates that project for the first time. The `Finance.Api.UnitTests` project remains empty by deliberate design — every HTTP behavior worth testing is more usefully exercised at the integration-test level where the real request pipeline runs. The default `WeatherForecastController.cs` and `WeatherForecast.cs` scaffold files are deleted as part of this feature — they were never part of the product surface and would otherwise show up in the OpenAPI document.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified.**

No violations. Section intentionally empty.
