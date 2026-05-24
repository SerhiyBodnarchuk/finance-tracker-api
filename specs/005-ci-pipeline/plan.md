# Implementation Plan: CI Pipeline for Pull Requests

**Branch**: `005-ci-pipeline` | **Date**: 2026-05-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-ci-pipeline/spec.md`

## Summary

Author a single GitHub Actions workflow file at `.github/workflows/ci.yml` that runs on every push to a pull request targeting `main` or `development`. The workflow runs on `ubuntu-latest`, installs the .NET 10 SDK, restores the `FinanceTracker.slnx` solution, builds it in **Release** configuration, then runs `dotnet test` against the same Release build (which covers all four test projects — three unit-test projects + `Finance.Api.IntegrationTests`). The workflow uses GitHub's `concurrency` group to cancel superseded in-flight runs and reports a single check on the pull request whose pass/fail is the AND of all steps.

The deliverable of this feature is **one YAML file**. No production C# code changes, no test additions, no new NuGet packages, no new project, no script files in `.specify/`. The branch-protection rule that marks this workflow's check as "required" is a maintainer-applied repository setting, not a file in this PR.

## Technical Context

**Language/Version**: GitHub Actions workflow syntax (YAML) — version-pinned via `actions/*@v4` references. The code being built is C# 13 on **.NET 10 (`net10.0`)**, which is the SDK the workflow installs.

**Primary Dependencies**: `actions/checkout@v4`, `actions/setup-dotnet@v4`, and `actions/cache@v4`. All three are first-party GitHub-published actions; no third-party marketplace actions are introduced. No new NuGet packages.

**Storage**: N/A. The workflow is stateless across runs except for the NuGet cache (an optional GitHub Actions cache entry keyed by `**/*.csproj` content hash, persisted by GitHub on the runner host, not in the repository).

**Testing**: The workflow's own correctness is verified by **running it** against a pull request — there is no unit-test layer for a YAML workflow file. Verification steps are listed in `quickstart.md` (open a green PR, observe pass; open a red PR with a deliberate compile or test break, observe fail; observe supersede-on-rapid-push behaviour).

**Target Platform**: GitHub-hosted `ubuntu-latest` runner. Linux only by design — the user explicitly asked for Linux, and the project has no Windows-specific test coverage that would warrant a matrix.

**Project Type**: **Build infrastructure / developer tooling**. Not a library, not a service. The artifact is configuration consumed by GitHub Actions.

**Performance Goals**: Cold-cache full run under 10 minutes (SC-002). Warm-cache full run should land under 4 minutes once NuGet packages are cached (informational target, not a spec requirement). Pipeline start latency under 1 minute (SC-001).

**Constraints**: Must run with **zero secrets** (FR-009, SC-005) so fork PRs work end-to-end. Must NOT introduce a database, container, or external service. Must NOT depend on any non-`@v4` first-party or any third-party marketplace action — keeping the surface auditable. Must NOT add a `global.json` to the repository (out of scope for this feature; SDK version is pinned inside the workflow only).

**Scale/Scope**: One workflow file (~60 lines), one workflow definition, one job. Expected lifetime: every PR ever opened against this repository.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against `.specify/memory/constitution.md` v3.0.0. This feature touches **no C#**; the gates below evaluate whether the workflow honours the constitution's CI and scope clauses.

| # | Principle | Verdict | Notes |
|---|---|---|---|
| 1 | I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) | PASS | No source-tree edits. The workflow builds `FinanceTracker.slnx`, which already obeys the dependency direction; CI cannot weaken it. |
| 2 | II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) | PASS | Not applicable — no business code is touched. The workflow runs the existing test suite that already guards the pattern. |
| 3 | III. Seeded In-Memory Determinism (NON-NEGOTIABLE in spirit) | PASS | The integration tests use `WebApplicationFactory<Program>` with the in-memory repositories already wired in DI; CI introduces no database, container, or external service. |
| 4 | IV. Test-First with xUnit v3 | PASS | The workflow's test step is exactly `dotnet test --no-build -c Release`, which runs every xUnit v3 project under `tests/`. No FluentAssertions, no Moq additions, no Mvc.Testing additions — purely runs the existing suite. The constitution's CI clause (`restore` → `build --no-restore` → `test --no-build`) is honoured to the letter. |
| 5 | V. AI-Assisted Development Transparency | PASS | An entry in `ai-artifacts/agent_log.txt` will be appended for the AI work that produced this plan and workflow file. The workflow itself does not exfiltrate context or secrets (it has none); fork PRs run with default `GITHUB_TOKEN` scope. OpenAPI / Scalar are still dev-gated in `Program.cs` and are not invoked by the build. |
| - | Technology & Scope Constraints | PASS | Required stack honoured: workflow file lives at `.github/workflows/ci.yml` (the exact path the constitution names). SDK is .NET 10. No SQL Server, no EF Core, no LocalDB, no Docker, no auth, no UI, no payment integrations. Swashbuckle stays absent. |
| - | Dev Workflow & Quality Gates | PASS | The workflow IS the "CI gate" the constitution describes: it runs `dotnet restore` → `dotnet build --no-restore -c Release` → `dotnet test --no-build -c Release`. Branch-protection enforcement is a repo setting outside this feature's file scope but is acknowledged in [quickstart.md](quickstart.md). |

**Result: PASS, no violations to track.** The Complexity Tracking section below remains empty.

### Re-check after Phase 1 design

After authoring `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`, the gates above were re-evaluated. **No regressions.** One clarification surfaced and is captured in research.md:

- **R-001** — The constitution says CI runs `build --no-restore` and `test --no-build`. To honour both flags simultaneously, the workflow MUST issue three sequential commands (`restore`, then `build --no-restore -c Release`, then `test --no-build -c Release`). A single combined `dotnet test` (which implicitly restores and builds) would re-do work and would also build in `Debug` by default — both unacceptable. Recorded in research.md and reflected in [contracts/workflow-contract.md](contracts/workflow-contract.md).

## Project Structure

### Documentation (this feature)

```text
specs/005-ci-pipeline/
├── plan.md              # This file
├── research.md          # Phase 0 output — runner, SDK version, cache strategy, concurrency, triggers
├── data-model.md        # Phase 1 output — workflow-run state shape (owned by GitHub Actions; documented for traceability)
├── quickstart.md        # Phase 1 output — how to verify the workflow on a real PR
├── contracts/           # Phase 1 output
│   └── workflow-contract.md   # Job/step structure, inputs, outputs, exit conditions, the file's promised shape
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created by /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by this command)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── ci.yml                              # CREATED by this feature — the only deliverable
```

Everything else in the repository is untouched by `/speckit-implement` for this feature:

```text
src/backend/FinanceTracker/
├── FinanceTracker.slnx                     # untouched (the workflow consumes this)
├── Finance.Api/                            # untouched
├── Finance.Business/                       # untouched
├── Finance.Data/                           # untouched
└── tests/                                  # untouched — workflow runs whatever already lives here
    ├── Finance.Data.UnitTests/
    ├── Finance.Business.UnitTests/
    ├── Finance.Api.UnitTests/
    └── Finance.Api.IntegrationTests/

ai-artifacts/
└── agent_log.txt                           # APPENDED by /speckit-implement (one entry for this feature)
```

**Structure Decision**: The deliverable is **one YAML file** at `.github/workflows/ci.yml`. The constitution names that exact path; no alternative location is acceptable. The workflow targets the solution file `src/backend/FinanceTracker/FinanceTracker.slnx` from the repository root, setting `working-directory` once at the job level to avoid path repetition in every step. The Phase 1 contract documents the per-step shape; the implementer's only real choices are tactical (cache key composition, whether to capture a TRX log artifact — both addressed in research.md).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified.**

*No violations to track.* Constitution Check passes both pre- and post-design. R-001 in `research.md` is a clarification, not a deviation.
