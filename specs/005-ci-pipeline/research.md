# Phase 0 — Research: CI Pipeline for Pull Requests

**Feature**: `005-ci-pipeline`
**Plan**: [plan.md](plan.md)
**Date**: 2026-05-24

This document resolves the technical unknowns surfaced by the Technical Context section of `plan.md`. Each decision is final unless overturned by an amendment or by a later clarification recorded here.

---

## D1 — Runner image

**Decision**: `ubuntu-latest`.

**Rationale**: The user explicitly asked for Linux; `ubuntu-latest` is the GitHub-hosted Linux image with the broadest pre-installed toolchain (Git, curl, bash, recent .NET SDKs, the Mono runtime needed for some older tooling). It costs nothing for public repos and has the highest free-tier minute quota for private repos. No project-specific need for a custom container.

**Alternatives considered**:
- `ubuntu-22.04` / `ubuntu-24.04` (explicit pin): rejected. `ubuntu-latest` follows GitHub's currency policy and the project's "warn-driven" CI surface; nothing in the build depends on a specific OS minor version. We accept GitHub's deprecation cycle.
- `windows-latest`: rejected. The user asked for Linux; the project has no Windows-specific tests.
- Self-hosted runner: rejected. Adds infrastructure with no benefit for an MVP single-developer project.

---

## D2 — .NET SDK installation

**Decision**: Install the .NET 10 SDK explicitly with `actions/setup-dotnet@v4` and `dotnet-version: '10.0.x'`. Do **not** add a `global.json` to the repository.

**Rationale**: `ubuntu-latest` ships some .NET SDKs pre-installed but the set drifts over time; pinning via `setup-dotnet` guarantees that the workflow gets a .NET 10 SDK regardless of image rotation. The `'10.0.x'` floating-patch form keeps the workflow on the latest .NET 10 servicing release without re-editing the YAML every patch month. No `global.json` is required because the workflow is currently the only external consumer that cares about the SDK version; adding `global.json` would impose a developer-machine requirement (every contributor's `dotnet` would refuse to run against a higher major), which is out of scope per the plan's Constraints.

**Alternatives considered**:
- `dotnet-version: '10.0'` (no `.x`): rejected. NuGet/`setup-dotnet` treats this identically in current versions, but the `.x` form is the documented idiom for "latest patch in the minor band" and is clearer to readers.
- `dotnet-version: '10.0.100'` (full pin): rejected. Pins us to a specific patch and would need bumping every servicing release for no benefit.
- Add `global.json`: rejected (see rationale).
- Rely on pre-installed SDK on `ubuntu-latest`: rejected. Image rotation could remove or downgrade the .NET 10 SDK at any time and silently break CI.

---

## D3 — Trigger

**Decision**: `on: pull_request` with `branches: [main, development]`. No `push` trigger, no `workflow_dispatch`, no schedule.

**Rationale**: The spec is explicit — this pipeline guards pull requests. `branches:` filter on `pull_request` is GitHub's "PRs targeting these base branches" idiom; both `main` and `development` are currently active per the project's branch hygiene. A `push` trigger would double-run the workflow (once on the branch push, once on the PR sync) and would muddy the "PR gate" semantic. `workflow_dispatch` is intentionally omitted to keep the workflow's surface narrow; a maintainer needing to re-run can use GitHub's built-in "Re-run jobs" button on the PR check.

**Alternatives considered**:
- `on: push, pull_request`: rejected. Double-fires per PR push; the spec's edge case "rapid pushes" is solved by `concurrency`, not by adding triggers.
- `on: pull_request` with no `branches:` filter (all branches): rejected. The spec's success criteria scope the gate to protected base branches; opening a PR against an arbitrary feature branch should not block the merge of a separate, real PR.
- Add `workflow_dispatch`: rejected. Out of scope; the PR re-run button covers the rare case.

---

## D4 — Concurrency / supersession

**Decision**: At workflow level, set
`concurrency: { group: "ci-${{ github.workflow }}-${{ github.ref }}", cancel-in-progress: true }`.

**Rationale**: This satisfies FR-011 and SC-006. The group key uses `github.ref` (which for `pull_request` is `refs/pull/<n>/merge`), so two in-flight runs for the **same** PR collapse to "newest wins" while runs for **different** PRs remain independent. `cancel-in-progress: true` (vs. queueing) honours the spec's intent that reviewers see only the freshest signal.

**Alternatives considered**:
- `group: ${{ github.workflow }}` (no ref): rejected. Would serialize all PRs and waste developer time.
- `cancel-in-progress: false` (queue instead of cancel): rejected. Two stale results would still appear; spec wants only the latest.
- No `concurrency` block: rejected. Two pipelines for the same PR would both report a status; the more recent one's status would win only if the older one finished later — an unstable signal.

---

## D5 — Test execution strategy

**Decision**: A single `dotnet test src/backend/FinanceTracker/FinanceTracker.slnx --no-build --configuration Release --verbosity minimal` invocation runs **all four** test projects (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`, `Finance.Api.IntegrationTests`) at once. The step shows in the GitHub UI as a single line; per-test results appear in the log.

**Rationale**: The constitution mandates `restore` → `build --no-restore` → `test --no-build` exactly — it does **not** mandate two separate test invocations. Splitting unit and integration into separate steps or jobs is a perceived-clarity gain that costs duplicate restore/build context (or shared cache plumbing) and risks divergence (one step changing without the other). Keeping it as one invocation also matches the existing Spec Kit constitution wording and the `dotnet test` UX on a single-developer MVP.

**Alternatives considered**:
- Separate steps for unit vs. integration with `--filter` predicates: rejected. The project's xUnit `Trait`/`Category` taxonomy isn't established; introducing one just to split CI is out of scope. The spec accepts either approach (see User Story 3, Acceptance Scenario 3) but bundles them under one PR check.
- Parallel jobs (`unit-tests`, `integration-tests`): rejected. Doubles cache plumbing and runner minutes; the integration tests are in-process (`WebApplicationFactory`), so they're fast and don't benefit from job-level isolation.
- `dotnet test` without `--no-build`: rejected. Re-builds in Debug by default, contradicting D7 below and the constitution's CI clause.

---

## D6 — NuGet package cache

**Decision**: Use `actions/cache@v4` to cache `~/.nuget/packages`, keyed by `dotnet-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}` with a fallback restore-key `dotnet-${{ runner.os }}-`.

**Rationale**: Cuts cold-run restore time from ~30–60s to a few seconds on a cache hit; preserves SC-002 (under 10 min cold) and gives realistic SC-002 margins. Hashing `**/*.csproj` (rather than `packages.lock.json`, which the project doesn't use) keys the cache on the only file set whose change actually invalidates the package graph. The fallback restore-key allows a partial hit when `.csproj` files change.

**Alternatives considered**:
- `actions/setup-dotnet@v4`'s built-in `cache: true` option: rejected. That option requires `packages.lock.json` (NuGet lock files) which the project doesn't have. We'd need to enable `RestorePackagesWithLockFile` in every `.csproj` and commit lock files just to satisfy the action — out of scope and noisy.
- No caching: rejected. Cold-restore time is 30–60s and is wasted on every PR push.
- Cache `~/.nuget/packages` AND `~/.nuget/v3-cache`: rejected. The latter is the HTTP cache, which gives marginal extra benefit at the cost of cache-size pressure.

---

## D7 — Configuration: Release

**Decision**: `dotnet build` runs with `--configuration Release`; `dotnet test` runs with `--configuration Release --no-build` so it consumes the same artifacts.

**Rationale**: The user's request is literally "build+release". Release configuration in .NET enables optimizations and turns off debug-only assertions; testing the same artifacts you'd ship is the test-of-record property the user wants. The constitution's CI bullet doesn't pin a configuration, so we take the user's intent at face value.

**Alternatives considered**:
- Debug build: rejected by user's explicit request.
- Matrix `[Debug, Release]`: rejected. Doubles runtime for no extra defect-catching power on a single-developer MVP.
- `--no-restore` only on `build`, not `test`: rejected. `test --no-restore` (without `--no-build`) would re-build in Debug — see D5/D6.

---

## D8 — Restore / build / test command separation (R-001)

**Decision**: Three sequential steps:

1. `dotnet restore src/backend/FinanceTracker/FinanceTracker.slnx`
2. `dotnet build src/backend/FinanceTracker/FinanceTracker.slnx --no-restore --configuration Release`
3. `dotnet test src/backend/FinanceTracker/FinanceTracker.slnx --no-build --configuration Release --verbosity minimal`

**Rationale (R-001 in the plan)**: The constitution's CI clause names these three commands with these flags in this order. Honouring `--no-restore` on `build` requires a preceding `restore` step. Honouring `--no-build` on `test` requires a preceding `build` step at the same configuration. Collapsing into a single `dotnet test` (which implicitly restores + builds) would (a) violate the constitution's CI clause wording and (b) silently build the test step in Debug regardless of any `--configuration` argument on `test` alone if the restore-and-build step hadn't already produced Release outputs. Three explicit steps also give the spec's "single overall pass/fail status with per-step visibility" (SC-004) for free.

**Alternatives considered**:
- Single `dotnet test --configuration Release` (implicit restore + build): rejected per the above and per D5/D7.
- Two steps (`restore + build` combined, `test` separate): rejected. Loses the per-step status signal that satisfies SC-004 and contradicts the constitution's three-command wording.

---

## D9 — `.slnx` (next-gen solution format) support

**Decision**: Pass the explicit solution path `src/backend/FinanceTracker/FinanceTracker.slnx` to every `dotnet` command rather than relying on directory inference.

**Rationale**: `.slnx` (XML-format solution file) has first-class support starting with .NET 9 (`dotnet sln` migration command) and is stable in .NET 10. Passing the explicit path makes the workflow self-documenting and removes any ambiguity if a `.sln` is ever added next to the `.slnx`. The `working-directory` could be set at the job level (`src/backend/FinanceTracker`) to shorten paths, but the explicit-path form survives directory restructures better.

**Alternatives considered**:
- Set `defaults: { run: { working-directory: src/backend/FinanceTracker } }` at the workflow level: viable; saves a few characters per step. Either form is acceptable to the implementer. Documenting the trade-off here to avoid a wrangle in PR review.
- Rely on `dotnet` auto-discovery in the current directory: rejected. Implicit and fragile.

---

## D10 — Permissions

**Decision**: Set `permissions: { contents: read }` at the workflow level. No other scopes.

**Rationale**: The workflow only reads source code; it does not write commits, push tags, post comments, or publish artifacts. The minimum-scope token follows the GitHub principle of least privilege and is the strongest signal that fork PRs (which run with a read-only `GITHUB_TOKEN` by default) will work identically to internal PRs.

**Alternatives considered**:
- Default token permissions (whatever the repo setting is): rejected. Implicit and varies by repository settings; explicit narrowing is safer.
- Add `pull-requests: write` (for status comments): rejected. The default check API already surfaces the run status on the PR; no comment posting is needed.

---

## D11 — Action versions and pinning

**Decision**: Use `@v4` major-version tags for `actions/checkout`, `actions/setup-dotnet`, and `actions/cache`. Do **not** pin to a full commit SHA in this MVP.

**Rationale**: All three are first-party GitHub actions with a strong track record on `@v4`; floating major-version tags get security and bug-fix updates within the major. SHA-pinning would harden the supply chain at the cost of dependabot churn — appropriate for a high-stakes production repo, overkill for a single-developer MVP. The "no third-party marketplace actions" constraint stands.

**Alternatives considered**:
- Pin to commit SHA (e.g., `actions/checkout@b4ffde...`): rejected. Too much maintenance for the threat model.
- Use `@main`: rejected. No version stability; would break unpredictably.
- Use older `@v3`: rejected. `v4` is current and supports the `actions/cache@v4` cache-key/restore-keys behaviour we rely on.

---

## D12 — Logging verbosity and test report

**Decision (revised three times on 2026-05-24)**:
- `dotnet build`: `--verbosity minimal`.
- `dotnet test`: drop `--verbosity minimal`. Append `-- --report-trx --results-directory ${{ github.workspace }}/TestResults` (MTP-native flags after the `--` stop-parsing token).
- New step 7: `actions/upload-artifact@v4` uploads the workspace-rooted `TestResults/` directory under the artifact name `test-results`.

**Rationale (revision 3 — the load-bearing one)**: The project's test projects reference **`xunit.v3` only** (no `Microsoft.NET.Test.Sdk`, no `xunit.runner.visualstudio`) and run as `OutputType=Exe`. That makes them **pure-MTP (Microsoft.Testing.Platform)** test platforms, not VSTest. The consequence: the VSTest `--logger trx` flag is silently ignored by these projects (no error, no TRX, just an empty `TestResults/`). The MTP-native equivalent is `--report-trx`, but it must be passed *after* the `--` stop-parsing token so `dotnet test` forwards it to the MTP runner rather than trying to interpret it itself. Pinning `--results-directory` to `${{ github.workspace }}/TestResults` keeps every project's TRX in a single, predictable place so the upload step doesn't have to glob deep into each test project's `bin/Release/net10.0/TestResults/`.

xUnit v3's MTP runner prints a per-project pass/fail summary to stdout by default; no extra `--logger console` flag is needed for inline visibility (and adding one would be ignored anyway, for the same reason `--logger trx` was).

**Revision history**:
- **Revision 1** (initial test-report amendment): tried `--logger trx;LogFileName=test-results.trx` + an inline `python3` parser writing to `$GITHUB_STEP_SUMMARY`. Reverted in revision 2 for being too custom (~25 lines of bespoke XML-parsing in YAML).
- **Revision 2**: kept `--logger trx` + `--logger "console;verbosity=normal"`, replaced the parser with `actions/upload-artifact@v4`. Looked clean. **It didn't work** — the first live CI run produced "No files were found with the provided path: src/backend/FinanceTracker/**/TestResults/*.trx", because both `--logger` flags were VSTest-only and silently ignored by xUnit v3's MTP runner.
- **Revision 3 (current)**: replaced both `--logger` flags with the MTP-native `-- --report-trx --results-directory <abs path>`. Adjusted the artifact's `path` to match the chosen absolute results directory.

**Alternatives considered (revision 3)**:
- Add `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` to the test projects to re-enable the VSTest-style `--logger trx` path: rejected. That's a production-side change just to make CI work, and it adds two NuGet packages the project deliberately doesn't reference today. The MTP-native flag set is the supported, idiomatic xunit.v3 path.
- Use `dotnet test -- --report-trx` without `--results-directory` and let each project's TRX land in `bin/Release/net10.0/TestResults/`: rejected. The upload glob would have to be `src/backend/FinanceTracker/**/TestResults/*.trx` — works, but is more fragile than a single pinned directory.
- Use `${{ runner.temp }}/TestResults` instead of `${{ github.workspace }}/TestResults`: rejected. `runner.temp` is outside the workspace and `actions/upload-artifact`'s `path:` becomes harder to reason about (it would need to be an absolute path, which is allowed but less idiomatic).
- Set a custom `retention-days` on the artifact: rejected. The repository default is fine; tuning that knob is out of scope for the MVP.
- Third-party reporter actions (e.g. `dorny/test-reporter`, `EnricoMi/publish-unit-test-result-action`): still rejected, for the same C3 / C7 reasons as before.

---

## D13 — `workflow_dispatch` (manual trigger)

**Decision**: Not included.

**Rationale**: The spec is PR-scoped. Manual runs are covered by GitHub's "Re-run jobs" button on a finished or cancelled run. Adding `workflow_dispatch` would also raise a separate question about what `ref` the manual run targets, which the spec does not address.

**Alternatives considered**:
- Add `workflow_dispatch` for ad-hoc validation on any branch: rejected as scope creep.

---

## Summary table

| Decision | Choice |
|----------|--------|
| D1 Runner | `ubuntu-latest` |
| D2 SDK install | `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`; no `global.json` |
| D3 Trigger | `pull_request` on `[main, development]` only |
| D4 Concurrency | per-ref group, `cancel-in-progress: true` |
| D5 Test split | single `dotnet test` invocation (all four test projects) |
| D6 NuGet cache | `actions/cache@v4` on `~/.nuget/packages`, keyed on `**/*.csproj` hash |
| D7 Build config | `Release` everywhere |
| D8 Step layout | three discrete steps: restore, build `--no-restore`, test `--no-build` |
| D9 Solution path | explicit `src/backend/FinanceTracker/FinanceTracker.slnx` |
| D10 Permissions | workflow-level `contents: read` only |
| D11 Action pinning | `@v4` major tags; no SHA pinning |
| D12 Verbosity | `--verbosity minimal` |
| D13 Manual trigger | not included |
