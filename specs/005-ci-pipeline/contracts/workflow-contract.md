# Workflow Contract: `.github/workflows/ci.yml`

**Feature**: `005-ci-pipeline`
**Plan**: [../plan.md](../plan.md)
**Research**: [../research.md](../research.md)
**Date**: 2026-05-24

This document is the implementation-side contract for `.github/workflows/ci.yml`. The implementer of this feature MUST produce a workflow file that satisfies every clause below; `/speckit-tasks` will derive tasks from it; `/speckit-implement` will write the YAML.

The contract is **prescriptive on shape and behaviour**, not on syntactic minutiae (whitespace, comment placement, the exact name string of an optional cache step). Where research.md fixed a decision (D1–D13), that decision is incorporated here without re-justification.

---

## C1 — File location and metadata

- **Path**: `.github/workflows/ci.yml` (relative to repository root). No other location is acceptable.
- **Top-level `name:`**: `"CI"`. This is the string that appears as the Check Run name on every PR; reviewers grep for it. Do not rename.
- **Encoding**: UTF-8, LF line endings. No BOM.

## C2 — Trigger

```yaml
on:
  pull_request:
    branches: [main, development]
```

- No `push` trigger.
- No `workflow_dispatch`.
- No schedule.
- No `paths:` filter — the workflow runs on every PR push, including docs-only PRs (spec edge case #5).

## C3 — Workflow-level permissions

```yaml
permissions:
  contents: read
```

- No additional scopes. The token MUST be read-only.

## C4 — Concurrency

```yaml
concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

- Group key is per-ref (per D4), so different PRs do not preempt each other.
- `cancel-in-progress: true` is mandatory (SC-006, FR-011).

## C5 — Job structure

Exactly one job, named `build-and-test`.

```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    defaults:
      run:
        working-directory: src/backend/FinanceTracker
    steps:
      # see C6
```

- `runs-on: ubuntu-latest` per D1. No matrix.
- `timeout-minutes: 15` — defensive guardrail above the 10-minute SC-002 budget; not a tuning knob.
- `defaults.run.working-directory` keeps step bodies short (D9). The `actions/*` steps that don't take a `run:` block are unaffected by this default.

## C6 — Steps (ordered)

The job MUST contain exactly these **seven** steps in this order, with these names. Optional fields not listed below MAY be added by the implementer if they don't change observable behaviour; mandatory fields below MUST appear.

> **Amendment 2026-05-24**: step 6 was extended with two `--logger` flags and a new step 7 (`Upload test results`) was added in response to the request "need to see how many tests passed". A first revision used inline Python to render a Markdown summary; a second revision (same day) replaced that bespoke parser with `actions/upload-artifact@v4` so the TRX files are simply uploaded as a downloadable artifact. The current contract reflects the second revision. Both revisions are captured in research.md D12-revised and in `ai-artifacts/agent_log.txt`.

### Step 1 — `Checkout`

```yaml
- name: Checkout
  uses: actions/checkout@v4
```

- No `fetch-depth` override; default shallow clone is sufficient (the build doesn't need history).

### Step 2 — `Setup .NET 10`

```yaml
- name: Setup .NET 10
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'
```

- `dotnet-version` MUST be `'10.0.x'` (floating-patch form, per D2).
- No `cache:` argument here (we use a separate `actions/cache` step — D6 rationale).

### Step 3 — `Restore NuGet cache`

```yaml
- name: Restore NuGet cache
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: dotnet-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      dotnet-${{ runner.os }}-
```

- Cache path is `~/.nuget/packages` (the global NuGet packages folder on the runner).
- Key hashes every `**/*.csproj` so any project-file change invalidates the cache; the `restore-keys` fallback allows a partial hit when only one csproj changed (D6).
- This step is in the `defaults` working-directory but doesn't care; the cache path is absolute (`~/`).
- **Never fails the run** — a cache miss is silent.

### Step 4 — `Restore solution`

```yaml
- name: Restore solution
  run: dotnet restore FinanceTracker.slnx
```

- Operates in `src/backend/FinanceTracker/` via the job's `working-directory` default (C5).
- No `--no-cache` — the previous step's cache is the whole point.
- Failure fails the run (default behaviour).

### Step 5 — `Build (Release)`

```yaml
- name: Build (Release)
  run: dotnet build FinanceTracker.slnx --no-restore --configuration Release --verbosity minimal
```

- `--no-restore` MUST be present (the previous step did the restore — constitution clause).
- `--configuration Release` MUST be present (D7, user requirement).
- `--verbosity minimal` per D12.

### Step 6 — `Test (Release)`

```yaml
- name: Test (Release)
  run: dotnet test FinanceTracker.slnx --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=test-results.trx"
```

- `--no-build` MUST be present (the previous step built — constitution clause).
- `--configuration Release` MUST be present (D7) — together with `--no-build`, this points `dotnet test` at the Release-built binaries.
- Runs all four test projects in one invocation (D5).
- Two `--logger` flags are mandatory:
  - `console;verbosity=normal` — surfaces per-project `Passed!  - Failed: 0, Passed: N, Skipped: 0` lines in the step's inline log (FR-012 + test-report visibility).
  - `trx;LogFileName=test-results.trx` — emits a TRX file per test project under that project's `TestResults/` directory; consumed by step 7.
- Failure fails the run (default behaviour). Tests must still short-circuit if step 5 failed (no `if: always()` here).

### Step 7 — `Upload test results`

```yaml
- name: Upload test results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: test-results
    path: src/backend/FinanceTracker/**/TestResults/*.trx
    if-no-files-found: ignore
```

- `if: always()` MUST be present. This is the **only** step in the workflow allowed to use `if: always()`; without it, a failed test run would not upload the TRX files developers need to diagnose the failure. C7 is amended below to reflect this carve-out.
- `actions/upload-artifact@v4` is a **first-party GitHub action** (same family as the other three used in this workflow); no third-party marketplace action is introduced.
- `path:` uses an absolute repo-rooted glob (not the job's `working-directory` default — `upload-artifact` does not consume `defaults.run.working-directory` because it's not a `run:` step).
- `if-no-files-found: ignore` MUST be present. When the build fails before any test runs, no TRX files exist and the step would otherwise warn or fail; `ignore` makes the step a no-op in that case so it does not affect the run's overall conclusion.
- Default retention (the repository's setting, typically 90 days) is acceptable. Do NOT add `retention-days:` — it's a tuning knob that doesn't belong in this MVP.
- The step MUST NOT inline-parse the TRX, write to `$GITHUB_STEP_SUMMARY`, or post check-run annotations. Consumption of the TRX is the developer's responsibility (download from the run page, open in Visual Studio / a TRX viewer, or feed into downstream tooling). Inline pass/fail counts are already visible in step 6's log via `--logger "console;verbosity=normal"`.

## C7 — What the workflow MUST NOT contain

The following clauses harden the feature against scope creep during implementation. A PR that adds any of them MUST be rejected unless an amendment to this contract lands first.

- **No `push:` trigger.** PRs only.
- **No matrix.** Single Linux runner only.
- **No `secrets.*` references.** The whole workflow MUST work with the default `GITHUB_TOKEN` only (FR-009, SC-005).
- **No third-party marketplace actions.** Only `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/cache@v4`.
- **`actions/upload-artifact` is permitted only in step 7, only for the `*.trx` files emitted by step 6.** No other artifact upload (coverage reports, build binaries, publish output, screenshots, anything else) is permitted. The artifact name MUST be `test-results`.
- **No `dotnet publish`** step. Build only, no packaging.
- **No `dotnet format`, `dotnet test --collect`, or linting steps.** Out of scope.
- **No `if: always()`** on the build/test steps (steps 1–6). Earlier failure MUST short-circuit them (US2 acceptance scenario 3). The only carve-out is **step 7 (Upload test results)**, which MUST use `if: always()` so a failed run still uploads whatever TRX files exist.
- **No `continue-on-error: true`** anywhere. Every step's failure must fail the run.
- **No `permissions:` widening beyond `contents: read`.**
- **No `services:`** block (no Docker side-cars; no Postgres, no Redis).
- **No `env:` block at workflow or job level** for this MVP. Step-level `env:` may be added if needed by a specific command, but none is needed today.
- **No `global.json`** is added to the repository as part of this feature (D2).

## C8 — Observable outcomes the contract guarantees

| Spec ID | Guarantee | Where it shows up |
|---------|-----------|-------------------|
| FR-001 | Workflow fires on every PR push to `main`/`development` | C2 |
| FR-002 | Linux runner | C5 (`runs-on: ubuntu-latest`) |
| FR-003 | Restore is a discrete first build-related step | C6 step 4 |
| FR-004 | Release build | C6 step 5 |
| FR-005 / FR-006 | All four test projects run | C6 step 6 (single `dotnet test` invocation over the solution) |
| FR-007 / FR-008 | Single PR status, AND of all steps | C5 (single job, default sequential semantics) |
| FR-009 | Zero secrets | C3 + C7 |
| FR-010 | File at `.github/workflows/ci.yml` | C1 |
| FR-011 | Cancel superseded runs | C4 |
| FR-012 | Failing-test name in log | C6 step 6 + xUnit v3 default logger |
| SC-001 | Start within 1 min | C2 (PR trigger fires immediately; GitHub queueing is normally <10s) |
| SC-002 | Full run ≤ 10 min | C5 timeout, D6 cache strategy |
| SC-003 | 100% broken PRs blocked | C8 default sequential failure short-circuit + maintainer-side branch protection (out of YAML scope) |
| SC-004 | Per-step status visible | C6 step names + GitHub's per-step UI |
| SC-005 | Fork PRs work | C3 + C7 (no secrets, no third-party actions) |
| SC-006 | Newest run wins | C4 |

## C9 — Out of contract / future work

These deliberately fall outside this feature and would warrant a separate spec:

- TRX log artifact upload for downstream test-report tooling.
- Coverage collection (`coverlet.collector` already referenced in some test projects could be wired up, but the spec excludes it).
- A `release.yml` workflow that builds NuGet packages on a tag push.
- Branch-protection-rule provisioning via `gh api` or Terraform — currently a manual repo setting.
- A matrix expansion to `windows-latest` if/when Windows-specific test coverage lands.
