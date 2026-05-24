---

description: "Task list for the CI Pipeline for Pull Requests feature"
---

# Tasks: CI Pipeline for Pull Requests

**Input**: Design documents from `specs/005-ci-pipeline/`
**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required), [research.md](research.md), [data-model.md](data-model.md), [contracts/workflow-contract.md](contracts/workflow-contract.md), [quickstart.md](quickstart.md)

**Tests**: This feature's deliverable is a single YAML workflow file. There are **no automated tests** for the workflow itself (a workflow YAML has no unit-test layer; verification happens by running it against PRs per [quickstart.md](quickstart.md)). Therefore, no test-tasks are generated. The spec does not request TDD.

**Organization**: The deliverable is exactly one file (`.github/workflows/ci.yml`), so the three P1 user stories cannot be implemented in three separate files. They are organized as incremental writes to that single file, each followed by a quickstart verification gate. **All three user stories must be merged in one PR**; the phase structure is for ordering the implementer's work, not for independent shipping.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies). Most tasks in this feature touch the same file (`.github/workflows/ci.yml`) so [P] is rare.
- **[Story]**: Which user story the task serves (US1, US2, US3 from spec.md).
- File paths are absolute from repository root.

## Path Conventions

This feature touches:
- `.github/workflows/ci.yml` — the single deliverable
- `ai-artifacts/agent_log.txt` — append-only AI log (constitution V)

No `src/` or `tests/` paths are touched.

---

## Phase 1: Setup

**Purpose**: Verify prerequisites and confirm the target directory exists. No scaffolding tasks (the `.github/workflows/` directory already exists).

- [X] T001 Verify `.github/workflows/` directory exists at the repository root (it should — `git status` already shows it). If absent, create it. Do NOT create any other file in this phase.
- [X] T002 Confirm no pre-existing `.github/workflows/ci.yml` is present. If one exists, STOP and reconcile with the user before overwriting — an existing file means another work stream produced a CI file outside this spec.
- [X] T003 Re-read [contracts/workflow-contract.md](contracts/workflow-contract.md) sections C1–C9. The implementation in Phase 2–5 follows it clause-by-clause.

**Checkpoint**: Setup complete. The repository is ready to receive the new workflow file.

---

## Phase 2: Foundational (Workflow Scaffolding)

**Purpose**: Author the workflow's non-step structure — `name`, `on`, `permissions`, `concurrency`, the job skeleton with `runs-on`, `timeout-minutes`, `defaults`, and the `Checkout` + `Setup .NET 10` steps. None of the user stories can be observed until this scaffolding exists, so it is foundational.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete (the build/test steps cannot run without checkout and the .NET SDK).

- [X] T004 Create `.github/workflows/ci.yml` with top-level `name: CI` and the `on: pull_request` trigger filtered to `branches: [main, development]`, per contract C1 and C2.
- [X] T005 Add the workflow-level `permissions: contents: read` block to `.github/workflows/ci.yml`, per contract C3.
- [X] T006 Add the workflow-level `concurrency` block to `.github/workflows/ci.yml` with `group: ci-${{ github.workflow }}-${{ github.ref }}` and `cancel-in-progress: true`, per contract C4.
- [X] T007 Add the `jobs.build-and-test` skeleton to `.github/workflows/ci.yml` with `runs-on: ubuntu-latest`, `timeout-minutes: 15`, and `defaults.run.working-directory: src/backend/FinanceTracker`, per contract C5.
- [X] T008 Add Step 1 (`name: Checkout`, `uses: actions/checkout@v4`) under the job's `steps:` list, per contract C6 step 1.
- [X] T009 Add Step 2 (`name: Setup .NET 10`, `uses: actions/setup-dotnet@v4`, `with.dotnet-version: '10.0.x'`) under the job's `steps:` list, per contract C6 step 2 and research D2.
- [X] T010 Add Step 3 (`name: Restore NuGet cache`, `uses: actions/cache@v4`) with `path: ~/.nuget/packages`, `key: dotnet-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}`, and the documented `restore-keys:` fallback, per contract C6 step 3 and research D6.

**Checkpoint**: The workflow can be triggered by opening a draft PR, but it will currently be a no-op job (checkout + SDK install + cache restore). It will appear in the PR's Checks panel; it will **not** yet build or test anything. This is the moment to verify the trigger filter and the runner image work before adding the actual build/test commands.

---

## Phase 3: User Story 1 - Pull request gated by an automated build (Priority: P1) 🎯 MVP

**Goal**: A PR's `CI` check goes red when the source code doesn't compile in Release configuration, and green when it does. Restore is a discrete first step before build.

**Independent Test**: Per [quickstart.md](quickstart.md) V2 — push a deliberate compile error to a PR branch and observe the `Build (Release)` step turn red while `Restore solution` stays green; revert and observe both turn green. The `Test (Release)` step is not yet present, so its absence is expected at this phase boundary.

### Implementation for User Story 1

- [X] T011 [US1] Add Step 4 (`name: Restore solution`, `run: dotnet restore FinanceTracker.slnx`) under the job's `steps:` list in `.github/workflows/ci.yml`, per contract C6 step 4. The `working-directory` default from T007 applies; do not repeat the path.
- [X] T012 [US1] Add Step 5 (`name: Build (Release)`, `run: dotnet build FinanceTracker.slnx --no-restore --configuration Release --verbosity minimal`) under the job's `steps:` list in `.github/workflows/ci.yml`, per contract C6 step 5 and research D7/D8/D12.
- [ ] T013 [US1] **DEFERRED to PR review** — On the `005-ci-pipeline` branch (or a child PR thereof), execute [quickstart.md](quickstart.md) V2 against `development` and confirm the red-then-green behaviour. Capture the resulting Workflow Run URL in a comment of the implementing PR for traceability.

**Checkpoint**: User Story 1 acceptance scenarios pass. The pipeline restores, builds in Release, and reports correctly to the PR. Tests are not yet run (intentional — Phase 4 adds them).

---

## Phase 4: User Story 2 - Unit tests run on every pull request (Priority: P1)

**Goal**: A PR's `CI` check goes red when any unit test fails, and green when all pass. The test step short-circuits if the build step fails (User Story 2 AC3).

**Independent Test**: Per [quickstart.md](quickstart.md) V3 — push a deliberately failing unit assertion (e.g., in `Finance.Business.UnitTests`), confirm the `Test (Release)` step turns red and the failing test's fully-qualified name appears in the log; revert and observe green.

### Implementation for User Story 2

- [X] T014 [US2] Add Step 6 (`name: Test (Release)`, `run: dotnet test FinanceTracker.slnx --no-build --configuration Release --verbosity minimal`) under the job's `steps:` list in `.github/workflows/ci.yml`, per contract C6 step 6 and research D5/D8/D12. This single command runs all four test projects under `src/backend/FinanceTracker/tests/`.
- [ ] T015 [US2] **DEFERRED to PR review** — On the `005-ci-pipeline` branch (or a child PR thereof), execute [quickstart.md](quickstart.md) V3 with a deliberately failing unit assertion in any of the three unit-test projects (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, or `Finance.Api.UnitTests`). Confirm red + visible test name; revert; confirm green. Capture both Workflow Run URLs in the PR.
- [ ] T016 [US2] **DEFERRED to PR review** — Verify that when the **Build (Release)** step is broken (e.g., re-introduce a compile error), the **Test (Release)** step does NOT execute — its row in the Checks panel must show as skipped/grey, not red. This satisfies User Story 2 acceptance scenario 3.

**Checkpoint**: User Story 2 acceptance scenarios pass. Unit tests gate the PR; the test step is properly downstream of the build step.

---

## Phase 5: User Story 3 - Integration tests run on every pull request (Priority: P1)

**Goal**: The same pipeline that runs unit tests also runs `Finance.Api.IntegrationTests` against the Release build, and reports a single combined status.

**Note**: **No new file edits are required for this phase.** Research decision D5 made unit and integration tests share a single `dotnet test` invocation (T014). Phase 5 is verification-only: prove the same step also runs the integration suite and reports correctly when an integration test fails.

**Independent Test**: Per [quickstart.md](quickstart.md) V3 (rerun) — push a deliberately failing assertion in `Finance.Api.IntegrationTests` and confirm the `Test (Release)` step turns red with the integration test's fully-qualified name in the log.

### Implementation for User Story 3

- [ ] T017 [US3] **DEFERRED to PR review** — On the `005-ci-pipeline` branch (or a child PR thereof), execute the [quickstart.md](quickstart.md) V3 walkthrough a second time, but this time break an assertion specifically in `Finance.Api.IntegrationTests` (e.g., change an expected status code). Confirm the `Test (Release)` step turns red and the failing **integration** test's fully-qualified name appears in the log. Revert; confirm green. Capture both Workflow Run URLs in the PR.
- [ ] T018 [US3] **DEFERRED to PR review** — On the same PR's green build, expand the `Test (Release)` step's log and confirm all four test projects' results are visible (count of tests passed per project). This proves the single invocation covers both unit and integration projects per research D5.

**Checkpoint**: User Story 3 acceptance scenarios pass. Integration tests gate the PR via the same step that gates unit tests. All three P1 user stories are now independently observable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate cross-cutting requirements (concurrency cancellation, fork-PR equivalence, edge cases), satisfy constitution Principle V (AI log entry), and acknowledge the maintainer-side branch-protection wiring.

- [ ] T019 [P] **DEFERRED to PR review** — Execute [quickstart.md](quickstart.md) V1 (green-PR smoke test) on the implementing PR. Confirm: `CI` check appears within 1 minute (SC-001); finishes inside the 10-minute budget on a cold cache (SC-002); all six step names from contract C6 are visible per-step in the Checks panel (SC-004).
- [ ] T020 [P] **DEFERRED to PR review** — Execute [quickstart.md](quickstart.md) V4 (rapid-push supersession) on the implementing PR. Confirm: an older in-flight run shows `conclusion: cancelled` and only the newest run reports a final status on the PR (SC-006, FR-011).
- [ ] T021 [P] **DEFERRED to PR review** — Execute [quickstart.md](quickstart.md) V5 (fork-PR equivalence) if practical — skip if no second account is available. If executed, confirm the pipeline runs identically with no secret-related failures (SC-005, FR-009).
- [X] T022 Append a single entry to `ai-artifacts/agent_log.txt` for this feature, with the six required fields (timestamp, model/tool, prompt summary, AI suggestion, decision, reason) per constitution Principle V. The "AI suggestion" field references this feature's plan + contract + the single `ci.yml` produced; "decision" is `accepted` (or `accepted with edits` if the implementer hand-tuned the YAML).
- [X] T023 Manually verify the final `.github/workflows/ci.yml` against [contracts/workflow-contract.md](contracts/workflow-contract.md) section C7 ("What the workflow MUST NOT contain"): no `push:` trigger, no matrix, no `secrets.*` references, no third-party marketplace actions, no `upload-artifact`, no `publish`, no `lint` step, no `if: always()`, no `continue-on-error: true`, no widened `permissions:`, no `services:` block, no workflow-level `env:`, no `global.json` added.
- [ ] T024 **DEFERRED to PR opening** — Update the PR description with a one-line callout to the maintainer: "After merge, mark the `CI` check as Required in branch-protection rules for `main` and `development` (see [quickstart.md](quickstart.md) V6). This is a one-time repo-settings change; the workflow YAML cannot perform it."

**Checkpoint**: Feature complete. All P1 user stories pass; cross-cutting outcomes are verified; the AI log and branch-protection follow-up are recorded.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. T004 → T005 → T006 → T007 → T008 → T009 → T010 are strictly sequential because they all edit the same file (`.github/workflows/ci.yml`).
- **User Story 1 (Phase 3)**: Depends on Foundational. T011 → T012 → T013, sequential (same file, then verification).
- **User Story 2 (Phase 4)**: Depends on User Story 1. T014 → T015 → T016, sequential.
- **User Story 3 (Phase 5)**: Depends on User Story 2 (uses the same `dotnet test` step). T017 → T018, sequential.
- **Polish (Phase 6)**: Depends on Phases 3–5. T019, T020, T021 are [P] (independent quickstart verifications that observe the same already-merged file). T022, T023, T024 are sequential housekeeping.

### Critical Path

```text
T001 → T002 → T003                       (Setup)
   → T004 → T005 → T006 → T007 → T008 → T009 → T010      (Foundational)
      → T011 → T012 → T013                                (US1)
         → T014 → T015 → T016                             (US2)
            → T017 → T018                                 (US3)
               → {T019, T020, T021}P → T022 → T023 → T024 (Polish)
```

### Parallel Opportunities

- **Within the workflow YAML edits (T004–T018)**: none. All edits target the same single file.
- **In Polish (T019, T020, T021)**: yes — three different quickstart scenarios run independently against the same already-merged workflow. A reviewer can do them in any order or in parallel during PR review.

---

## Parallel Example: Polish phase

```text
# These three quickstart verifications can be performed independently:
Task: T019 [P] — Quickstart V1 (green-PR smoke test)
Task: T020 [P] — Quickstart V4 (rapid-push supersession)
Task: T021 [P] — Quickstart V5 (fork-PR equivalence, optional)
```

No parallel opportunities exist in earlier phases — the YAML is one file with strictly ordered edits.

---

## Implementation Strategy

### Single-shot delivery (recommended)

Because the deliverable is one short YAML file, the pragmatic flow is:

1. Complete Phase 1 (T001–T003) — verify prerequisites.
2. Write the entire `ci.yml` in one editor pass, executing T004–T010, T011–T012, T014 in order without intermediate commits. (T013, T015–T018 are verifications, not edits — they cannot be combined into the file write.)
3. Commit and push. Open the PR.
4. Execute the verification tasks (T013, T015–T018, T019–T021) in order against the live PR, capturing run URLs.
5. Complete the housekeeping tasks (T022–T024).

### Incremental delivery (if reviewers want intermediate evidence)

If reviewers prefer per-user-story commits, you can split the YAML write into three commits:
1. **Phase 2 commit**: scaffolding (T004–T010). PR builds nothing but check appears.
2. **Phase 3 commit**: + restore + build (T011–T012). Execute T013.
3. **Phase 4 commit**: + test (T014). Execute T015–T018.
4. **Phase 6 commit**: housekeeping (T022).

This is more ceremony than the feature warrants but is acceptable.

---

## Notes

- This is the rare feature where "tasks per user story" does not yield per-story files. All three P1 stories live in the same YAML and must ship together; the phase split exists to order the implementer's edits and verifications.
- No new NuGet packages, no new test projects, no new C# files. If `/speckit-implement` is about to write any `.cs` file or any `.csproj` file, STOP — the implementation has drifted from the contract.
- Quickstart verification tasks (T013, T015–T021) require an actual PR to be open. If `/speckit-implement` runs them before the implementing PR exists, it cannot complete them — defer those tasks to the PR-review phase and mark them as outstanding.
- Constitution Principle V (agent_log.txt) is satisfied by T022. Skipping that task fails the constitution check; do not mark the feature complete without it.
- Branch-protection wiring is acknowledged in T024 but is **not** a YAML task — it's a repository-settings change the maintainer applies after the PR merges.
