# Implementation Plan: Seeded In-Memory Repositories

**Branch**: `002-in-memory-repositories` | **Date**: 2026-05-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-in-memory-repositories/spec.md`

## Summary

Deliver the **storage layer** that the rest of the MVP will read from: two repository interfaces (`ITransactionRepository`, `ICategoryRepository`) in `Finance.Data`, their seeded in-memory implementations (`InMemoryTransactionRepository`, `InMemoryCategoryRepository`), and the DI wiring in `Finance.Api/Program.cs` that registers both as **singletons**. Seed data is hard-coded inside the implementations and follows the contents documented in [spec.md §Key Entities](spec.md#key-entities): five categories (Salary, Groceries, Transport, Entertainment, Utilities) and five transactions covering one income + four expenses in May 2026. Behavior is intentionally narrow — the only invariant the repositories enforce is case-insensitive category-name uniqueness; every higher-level validation (`Amount > 0`, category referential integrity, multi-category compatibility with `TransactionType`) is deferred to the business layer in a later feature, as ratified by the spec's Assumptions section.

Technical approach: the two implementations are POCOs (no abstract base class) backed by a private `List<Transaction>` / `List<Category>` each. Identifiers are issued by an internal `int` counter that starts at the next available value above the seed range and is only incremented on a successful `Add` — so a rejected duplicate-name add **does not** consume an id, satisfying spec FR-007 and SC-005. The pre-seeded records use deterministic literal ids (categories 1–5, transactions 1–5). Reads return the underlying records directly (records are immutable by construction, so handing the stored reference back is safe); writes either append the stored form to the list and return it (success path) or return `null` (the typed "not found" signal for `GetById`) or `false` (delete miss). Concurrency is single-process serial; no locking is added (see [research.md §4](research.md#4-concurrency-stance)).

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`). Both `Finance.Data` and the test project `Finance.Data.UnitTests` already exist with nullable + implicit usings enabled; no csproj edits beyond DI wiring are needed.

**Primary Dependencies**: None beyond what feature 001 already pulled in. The repositories use built-in BCL types only (`List<T>`, `IReadOnlyCollection<T>`, `StringComparer.OrdinalIgnoreCase`). DI wiring uses `Microsoft.Extensions.DependencyInjection` types already transitively available to `Finance.Api`.

**Storage**: In-memory `List<T>` inside each repository instance. No file I/O, no JSON seed file, no external store. Seed contents are expressed as C# initializer lists in the implementation constructors — this keeps the seed data, the deterministic ids, and the production code that uses them in one place and trivially diffable.

**Testing**: **xUnit v3** (`xunit.v3` 3.2.2, `OutputType=Exe`) using the **built-in `Xunit.Assert` API**. **No FluentAssertions** (constitution Principle IV). New test files live in `Finance.Data.UnitTests/` (the project already has a `<ProjectReference>` to `Finance.Data`). Test coverage for this feature: every acceptance scenario from spec User Stories 1–3, the case-insensitive duplicate-name rule (FR-007) with the no-id-hole property (SC-005), and a 100-transaction insert + read-latency smoke test for SC-004.

**Target Platform**: Cross-platform .NET 10 runtime. Development host is Windows 11 + PowerShell; CI will run on the GitHub-hosted Ubuntu runner once `.github/workflows/ci.yml` lands (still out of scope here).

**Project Type**: Web service (ASP.NET Core Web API). This feature touches only `Finance.Data` (interfaces + implementations + seed) and `Finance.Api/Program.cs` (DI registration). `Finance.Business` is untouched — repositories belong to Data, not Business (constitution Principle I). API controllers are still not added in this feature; the repository registrations are wired into the existing `Program.cs` so that a follow-up controllers feature can simply inject `ITransactionRepository` / `ICategoryRepository`.

**Performance Goals**: Demo-grade. SC-004 sets the bar: 100 sequential inserts followed by reads, with every read under 50 ms on a developer machine. `List<T>.Where(...)` over a few-hundred-record store is more than enough — no indexing, no `Dictionary` lookup path. If a later feature pushes record counts into the thousands and the report strategies start hot-looping over the list, an `Add`-time dictionary index can be added then (see [research.md §3](research.md#3-lookup-data-structure)).

**Constraints**:

- Repositories MUST be registered as singletons (constitution Principle III). Anything else silently breaks the persistence-across-requests property the spec hinges on.
- Identifier issuance MUST be gap-free across rejected adds (spec FR-007, SC-005). Counter increments only after a successful append.
- Seeded records MUST use deterministic literal ids (constitution Principle III). Categories 1–5, transactions 1–5.
- `GetById` MUST return a clearly distinguishable "not found" — chosen idiom is `T?` (return `null`), consistent with the ai-artifacts spec's `Transaction? GetById(...)` signature and with how feature 003 (controllers) will translate it to HTTP 404.
- The repositories MUST NOT call into `Finance.Business` or `Finance.Api`. Cross-layer compatibility rules (category type ↔ transaction type) are explicitly out of scope (spec Assumptions).
- No third-party packages added.

**Scale/Scope**:

- 2 interfaces, 2 implementations, ~120 lines of production code, plus ~5 lines of DI registration in `Program.cs`.
- ~12 unit tests in `Finance.Data.UnitTests/Repositories/` (3 for seed shape, 4 for runtime add/lookup, 2 for delete semantics, 1 for case-insensitive duplicate rejection + no-id-hole, 1 for category not-found read, 1 perf smoke).
- Net source: ~150 lines production + ~250 lines tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Result of this gate (pre-Phase-0)**: ✅ **All five principles PASS against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.1.** No drifts to report. No Complexity Tracking entries are required.

Per-principle check:

| Principle | Status | Notes |
|---|---|---|
| I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) | ✅ **PASS** | Interfaces and implementations live in `Finance.Data` (constitution explicitly lists "repository interfaces, and seeded in-memory repository implementations" under Data's responsibilities). `Finance.Api` only touches `Program.cs` for DI composition — which is the API layer's documented responsibility. `Finance.Business` is not modified. Dependency direction `Api → Business → Data` is preserved; no reverse references. |
| II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) | ✅ **PASS** | This feature does not touch reports. The principle's invariants (typed envelope, summary `ReportResult`, multi-category attribution, sort order, ad-hoc-no-cache) are unaffected. The repositories' shape is consistent with what `PeriodReportStrategy` will later need: `GetAll()` returns the full transaction set so a strategy can filter by timestamp range and aggregate from there. |
| III. Seeded In-Memory Determinism (NON-NEGOTIABLE) | ✅ **PASS** | Singleton lifetime is wired in `Program.cs`. Identifiers are `int` (no `Guid`). Seed ids are deterministic literals (categories 1–5, transactions 1–5). `Transaction.Timestamp` from feature 001 is full `DateTime`; this feature does not change it. The repository surface accepts `Transaction.CategoryIds` (the non-empty multi-category list) as-is and does not enforce compatibility — enforcement is a Business-layer concern, exactly as the principle's "Validation enforcement of this rule is a Business-layer concern" bullet says. Case-insensitive duplicate-name rejection (`StringComparer.OrdinalIgnoreCase`) is implemented in `InMemoryCategoryRepository.Add`. No `currency` field, no persistent store, no migrations. |
| IV. Test-First with xUnit v3 | ✅ **PASS** | New tests live in `tests/Finance.Data.UnitTests/Repositories/`, use xUnit v3, and use `Xunit.Assert` only. The Principle IV coverage bullet **"Repository behaviour (singleton lifetime, seeded data shape, integer-ID determinism)"** is explicitly assigned to this feature and is the spine of the test list in [data-model.md §Tests](data-model.md#tests). No FluentAssertions, no Shouldly. |
| V. AI-Assisted Development Transparency | ✅ **PASS** | `ai-artifacts/agent_log.txt` will be appended for every non-trivial AI suggestion accepted/rejected during this feature. No real personal data in seed fixtures — seed transactions are `"Monthly salary"`, `"Uber Trip"`, `"Silpo Market"`, `"Netflix Subscription"`, `"Electricity Bill"` (generic, non-PII). OpenAPI / Scalar dev-only gating in `Program.cs` is preserved (the diff only adds two singleton registrations; the `if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.MapScalarApiReference(); }` block is left intact). |

**Result of this gate (post-Phase-1, re-check)**: ✅ **All five principles still PASS.** The Phase 1 designs in [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md) introduced no new dependencies, no new layer crossings, and no changes to the report or DTO shape. The repository interface signatures (`IReadOnlyCollection<T> GetAll()`, `T? GetById(int)`, `T Add(T)`, `bool Delete(int)` on transactions) are pure CRUD with no business semantics leaking in. Re-check unanimous.

## Project Structure

### Documentation (this feature)

```text
specs/002-in-memory-repositories/
├── plan.md                        # This file
├── research.md                    # Phase 0 — design decisions (seed timestamps, id generator,
│                                  #          lookup structure, concurrency stance, return idioms,
│                                  #          where validation lives)
├── data-model.md                  # Phase 1 — concrete repository interface signatures,
│                                  #          implementation outline, seed records, DI registration
├── contracts/
│   └── repository-contracts.md    # Phase 1 — public surface of ITransactionRepository and
│                                  #          ICategoryRepository (this feature has no HTTP
│                                  #          contracts; the C# interfaces ARE the contract)
├── quickstart.md                  # Phase 1 — how to build / run / test what this feature delivers
├── checklists/
│   └── requirements.md            # From /speckit-specify
├── spec.md                        # From /speckit-specify
└── tasks.md                       # Phase 2 (from /speckit-tasks, NOT created here)
```

### Source Code (repository root)

```text
src/backend/FinanceTracker/
├── FinanceTracker.slnx                       # Already references all 3 production + 4 test projects
├── Finance.Api/
│   ├── Finance.Api.csproj                    # Existing — no new package refs needed
│   └── Program.cs                            # MODIFIED — add two AddSingleton<...>() calls
├── Finance.Business/                         # UNTOUCHED by this feature
└── Finance.Data/
    ├── Finance.Data.csproj                   # Existing — no new package refs
    ├── Models/                               # From feature 001 — UNTOUCHED
    │   ├── Transaction.cs
    │   ├── Category.cs
    │   ├── TransactionType.cs
    │   └── CategoryType.cs
    └── Repositories/
        ├── ITransactionRepository.cs         # NEW — GetAll / GetById / Add / Delete
        ├── ICategoryRepository.cs            # NEW — GetAll / GetById / Add  (no Delete)
        ├── InMemoryTransactionRepository.cs  # NEW — seeded singleton implementation
        └── InMemoryCategoryRepository.cs     # NEW — seeded singleton implementation,
                                              #       enforces case-insensitive name uniqueness

tests/  (under src/backend/FinanceTracker/tests/)
└── Finance.Data.UnitTests/
    ├── Finance.Data.UnitTests.csproj         # Existing — ProjectReference to Finance.Data already in place
    └── Repositories/
        ├── InMemoryCategoryRepositoryTests.cs   # NEW — ~5 tests (seed shape, add, dup name reject, no-id-hole, not-found)
        └── InMemoryTransactionRepositoryTests.cs # NEW — ~7 tests (seed shape, add, get-by-id hit/miss,
                                                  #                 delete hit/miss, perf smoke 100-insert)
```

**Structure Decision**: Adds a single new folder `Finance.Data/Repositories/` to host both interfaces and both implementations side-by-side. Repositories are not split into separate sub-folders (e.g., `Repositories/Abstractions/` vs `Repositories/InMemory/`) because there are only four files and the project has no future plan for non-in-memory implementations within this MVP. Tests sit under a parallel `tests/Finance.Data.UnitTests/Repositories/` folder so the file tree mirrors the production layout — making test-to-production navigation predictable. DI composition lives entirely in `Finance.Api/Program.cs`; no `Finance.Data` `IServiceCollection` extension method is introduced because the two registration lines do not justify an extra abstraction (constitution Principle I would still permit it, but the "don't introduce abstractions beyond what the task requires" guidance argues against it).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified.**

No violations. Section intentionally empty.
