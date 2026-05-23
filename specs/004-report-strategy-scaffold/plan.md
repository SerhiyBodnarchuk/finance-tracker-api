# Implementation Plan: `report-strategy-scaffold` Claude Skill

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-report-strategy-scaffold/spec.md`

## Summary

Deliver a Claude Code skill, packaged at `.claude/skills/report-strategy-scaffold/`, that scaffolds a new report type into the existing factory + strategy pipeline. A successful run edits **four** project files (the `ReportType` enum, one new `*ReportData` record, one new `*ReportStrategy` class, and the DI registration block in `Finance.Api/Program.cs`), creates **two** test files (a unit-test file in `Finance.Business.UnitTests` and a new region in `Finance.Api.IntegrationTests/ReportsEndpointTests.cs`), and appends one entry to `ai-artifacts/agent_log.txt`. The skill runs `dotnet build` + `dotnet test` after editing and reports the outcome; refusal paths (duplicate strategy, persistence required, multi-range payload, missing anchor file) leave the working tree untouched.

The skill is implemented as a `SKILL.md` markdown prompt with frontmatter (per Claude Code skill spec) — no new C# code is added to the production solution **by this feature**. The skill is the deliverable; the production-code edits are what the skill *will produce* when invoked, not what `/speckit-implement` should write into the repo now.

## Technical Context

**Language/Version**: C# 13 on **.NET 10 (`net10.0`)**; skill manifest is plain Markdown with YAML frontmatter (no execution runtime of its own — Claude Code interprets it).

**Primary Dependencies**: None added by this feature. The skill output, when invoked, edits files that already depend on `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.Mvc.Testing`, `xunit.v3`, and `Moq` (4.20.x). No new NuGet packages are introduced.

**Storage**: N/A. The skill writes Markdown to disk and reads existing source files; there is no runtime data store.

**Testing**: Tests scaffolded by the skill use **xUnit v3** with `Xunit.Assert` and **Moq 4.20.x**; integration additions reuse `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` and the existing `ApiTestFixture` in `Finance.Api.IntegrationTests`. **No new test project is created.**

**Target Platform**: Claude Code (CLI / VS Code extension) on Windows, Linux, and macOS — wherever the developer's `dotnet` toolchain is. The skill must be platform-agnostic in the commands it shells out to: `dotnet build` and `dotnet test` are the only required external binaries.

**Project Type**: **Developer tooling** — a Claude Code skill that ships alongside the API repository. Not a library, not a service, not a CLI tool in the traditional sense.

**Performance Goals**: A scaffold run completes in **under 2 minutes** wall-clock, including `dotnet build` and `dotnet test`. (`dotnet build` warm: ~5s. `dotnet test` warm: ~10s. The remainder is the skill's analysis, file writes, and the LLM round-trips inherent to running inside Claude Code.)

**Constraints**: The skill MUST stay inside `.claude/skills/report-strategy-scaffold/`; it MUST NOT add new top-level files or new directories outside that path and the existing source/test trees. It MUST NOT shell out to `git` (the speckit `git` extension already handles commits via hooks; the skill avoids touching VCS state directly). It MUST NOT introduce out-of-scope dependencies (constitution: no DB, no Docker, no auth, no UI, no payment integrations).

**Scale/Scope**: Single skill, single repository, single developer at a time. Expected lifetime use: ~3–6 invocations during the MVP (one per new report type the project decides to add).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against `.specify/memory/constitution.md` v3.0.0. The skill itself produces no production code in this feature; the gates below evaluate **the code shape the skill is contracted to emit**, not the SKILL.md prompt.

| # | Principle | Verdict | Notes |
|---|---|---|---|
| 1 | I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) | PASS | Scaffold-produced edits land in `Finance.Business/Enums/ReportType.cs`, `Finance.Business/Dtos/Reports/<NewType>ReportData.cs`, `Finance.Business/Services/Reports/<NewType>ReportStrategy.cs`, and `Finance.Api/Program.cs` (DI registration only). No new file in `Finance.Data`; controllers and validators are not touched; the API layer still never sees a domain entity (the new strategy lives in Business and returns `ReportResult`). |
| 2 | II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) | PASS | Every scaffold targets exactly this pattern. The skill copies the parse → validate → resolve-to-range → filter → aggregate → sort shape from `PeriodReportStrategy`, generates a strategy that implements `IReportStrategy`, exposes `Type => ReportType.<NewType>`, and registers via `AddSingleton<IReportStrategy, <NewType>ReportStrategy>()` so the existing factory's `IEnumerable<IReportStrategy>` constructor auto-discovers it. `ReportResult` shape is preserved (six fields). FR-014(c) blocks payloads that can't reduce to one inclusive date range — i.e., enforces the invariant. |
| 3 | III. Seeded In-Memory Determinism (NON-NEGOTIABLE in spirit) | PASS | The skill makes no Data-layer edits. The new strategy reads from existing `ITransactionRepository` / `ICategoryRepository`, which are singleton in-memory implementations with deterministic int IDs (per `InMemoryTransactionRepository` / `InMemoryCategoryRepository`). No `Guid`, no `DateOnly` for `Transaction.Timestamp`, no `currency` field added or expected. |
| 4 | IV. Test-First with xUnit v3 | PASS | Every scaffold emits at least one new file under `Finance.Business.UnitTests/Services/Reports/` (mirroring `PeriodReportStrategyTests.cs`) and extends `Finance.Api.IntegrationTests/ReportsEndpointTests.cs`. Tests use `Xunit.Assert` only; collaborators are mocked with Moq 4.20.x; `FluentAssertions` is never referenced. Coverage areas exercised: report strategy selection by `ReportType` (already covered by `ReportStrategyFactoryTests` — the skill extends it for the new type), strategy aggregation, and category breakdown semantics (per the bullet "Category breakdown logic, including the income-first / expense-second / alphabetical-within-group ordering"). |
| 5 | V. AI-Assisted Development Transparency | PASS | FR-012 requires every invocation (accepted or rejected) to append one entry to `ai-artifacts/agent_log.txt` with the required six fields. Refusal paths (FR-014, FR-015) write a `decision: rejected` entry rather than fabricating a success. Dry-run mode (FR-017) writes nothing to the log, which is correct because no decision was made yet. |
| - | Technology & Scope Constraints | PASS | No new NuGet packages, no SQL/EF/Docker/Auth/UI introduced. The skill emits code that targets `net10.0` (the only TFM in the solution). Swashbuckle is not reintroduced; OpenAPI/Scalar wiring is untouched. |
| - | Dev Workflow & Quality Gates | PASS | FR-011 requires the skill to run `dotnet build` + `dotnet test` and report results. Refusal paths leave `git status --porcelain` empty (FR-015). Branch + commit hygiene is handled by the speckit git extension's `before_implement` / `after_implement` hooks. |

**Result: PASS, no violations to track.** The Complexity Tracking section below remains empty.

### Re-check after Phase 1 design

After authoring `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`, the gates above were re-evaluated. **No regressions.** Two clarifications surfaced and are captured in research.md:

- **R-001** — what "name already exists" means in FR-014(a). The constitution puts both `Period` and `IsoWeek` into the `ReportType` enum today but only ships a `PeriodReportStrategy`. The skill MUST distinguish "enum value present" from "strategy registered" and refuse only when a *strategy* (not just an enum value) already exists for the requested type. Recorded in research.md and reflected in [contracts/skill-input-output.md](contracts/skill-input-output.md).
- **R-002** — where DI registration lives. `ReportStrategyFactory` auto-discovers via `IEnumerable<IReportStrategy>`, but DI must still see each `IReportStrategy` implementation. That registration is in `Finance.Api/Program.cs` (one `AddSingleton<IReportStrategy, …>` line per strategy). The skill MUST edit this file to register the new strategy. The spec's wording at FR-007 ("MUST NOT touch the Finance.Api composition root if the existing factory already auto-discovers strategies") is therefore softened in the plan: the skill MUST touch exactly one line in `Program.cs` and is not permitted to touch anything else there. This is reflected in [contracts/file-writes.md](contracts/file-writes.md).

## Project Structure

### Documentation (this feature)

```text
specs/004-report-strategy-scaffold/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output — skill artifacts, generated-code shape, agent-log entry shape
├── quickstart.md        # Phase 1 output — IsoWeek worked example
├── contracts/           # Phase 1 output
│   ├── skill-input-output.md     # Skill arguments, prompts, refusal codes
│   ├── file-writes.md            # Exact paths the skill touches (accept path); exact "no-op" guarantee (refuse path)
│   └── agent-log-entry.md        # Schema of the agent_log.txt entry the skill appends
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created by /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by this command)
```

### Source Code (repository root)

```text
.claude/
└── skills/
    └── report-strategy-scaffold/        # NEW — the deliverable of this feature
        └── SKILL.md                     # YAML frontmatter + skill prompt

src/backend/FinanceTracker/
├── Finance.Api/
│   ├── Program.cs                       # EDITED by the skill at invocation time (one new AddSingleton line)
│   └── …                                # untouched
├── Finance.Business/
│   ├── Enums/
│   │   └── ReportType.cs                # EDITED by the skill at invocation time (one new enum value, if absent)
│   ├── Dtos/Reports/
│   │   ├── PeriodReportData.cs          # untouched
│   │   └── <NewType>ReportData.cs       # CREATED by the skill at invocation time
│   ├── Services/Reports/
│   │   ├── IReportStrategy.cs           # untouched (the contract)
│   │   ├── ReportStrategyFactory.cs     # untouched (auto-discovers via IEnumerable<IReportStrategy>)
│   │   ├── PeriodReportStrategy.cs      # untouched (the template)
│   │   └── <NewType>ReportStrategy.cs   # CREATED by the skill at invocation time
│   └── …                                # untouched
└── tests/
    ├── Finance.Business.UnitTests/
    │   └── Services/Reports/
    │       ├── PeriodReportStrategyTests.cs               # untouched
    │       ├── ReportStrategyFactoryTests.cs              # EDITED by the skill (a new factory-resolution test for the new ReportType)
    │       └── <NewType>ReportStrategyTests.cs            # CREATED by the skill at invocation time
    └── Finance.Api.IntegrationTests/
        └── ReportsEndpointTests.cs       # EDITED by the skill at invocation time (a new test region for the new type)

ai-artifacts/
└── agent_log.txt                        # APPENDED to by the skill at invocation time (one entry per invocation)
```

**Structure Decision**: The deliverable of this feature is a single new file: `.claude/skills/report-strategy-scaffold/SKILL.md`. Everything else in the tree above is **what the skill emits when invoked**, not what `/speckit-implement` writes during this feature. The plan documents both because the skill's correctness is defined by the file-write contract it must satisfy. The `Finance.*` source layout matches the constitution (Principle I, Technology & Scope Constraints) and is reproduced here so the skill's prompt can reference exact paths without re-deriving them at runtime.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified.**

*No violations to track.* Constitution Check passes; the Phase 1 re-check surfaced two clarifications (R-001, R-002) which are captured in `research.md` and the contracts rather than as constitution-amending complexity.
