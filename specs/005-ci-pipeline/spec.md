# Feature Specification: CI Pipeline for Pull Requests

**Feature Branch**: `005-ci-pipeline`

**Created**: 2026-05-24

**Status**: Draft

**Input**: User description: "Need to create a simple github pipeline for merge requests. It should run build+release, unit tests, integration tests. Linux is preferable"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pull request gated by an automated build (Priority: P1)

A contributor opens a pull request against the protected base branch. Before any reviewer is asked to read the diff, an automated pipeline restores dependencies and builds the solution in Release configuration on a Linux runner. The pipeline reports back to the pull request a single pass/fail signal for the build step.

**Why this priority**: A pull request that does not compile is the cheapest defect to catch and the most expensive to miss. This is the foundational gate — every other gate in this spec assumes a successful Release build has produced testable artifacts.

**Independent Test**: Open a pull request from a branch whose changes compile cleanly in Release. The pipeline must start automatically, complete the build step on a Linux runner, and surface a green check on the pull request. Open a second pull request that intentionally breaks compilation; the same pipeline must fail and surface a red check, blocking the pull request from being merged.

**Acceptance Scenarios**:

1. **Given** a pull request whose head branch compiles cleanly in Release, **When** the pipeline runs, **Then** the build step succeeds on a Linux runner and the pull request shows a passing status.
2. **Given** a pull request whose head branch introduces a compilation error, **When** the pipeline runs, **Then** the build step fails, the failing source location appears in the pipeline log, and the pull request shows a failing status.
3. **Given** a pull request from a freshly created branch with no prior runs, **When** the pipeline starts, **Then** dependency restoration completes before the build step begins.

---

### User Story 2 - Unit tests run on every pull request (Priority: P1)

After the build step succeeds, the same pipeline executes the project's unit test suites against the Release build artifacts. The pull request shows a single pass/fail signal that reflects whether the unit suites all passed.

**Why this priority**: Unit tests are the project's primary correctness gate per the constitution's Principle IV (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`). Skipping or deferring them defeats the whole reason the tests exist. P1 alongside the build because the two are inseparable in the project's "a feature is not done until tests pass" rule.

**Independent Test**: Open a pull request whose changes include a deliberately failing unit assertion. The pipeline must surface the failing test's fully-qualified name in the log and mark the pull request as failed. Revert the failing assertion and re-push; the pipeline must turn green.

**Acceptance Scenarios**:

1. **Given** a pull request where every unit test passes locally, **When** the pipeline runs the unit test step, **Then** all unit suites pass on the Linux runner and the pull request shows a passing status.
2. **Given** a pull request where at least one unit test fails, **When** the pipeline runs the unit test step, **Then** the failing test's name and failure message are visible in the pipeline log and the pull request shows a failing status.
3. **Given** the build step has failed, **When** the pipeline reaches the unit test step, **Then** the unit test step does not execute (no point running tests against a failed build).

---

### User Story 3 - Integration tests run on every pull request (Priority: P1)

After unit tests pass, the same pipeline executes the API integration test suite (`Finance.Api.IntegrationTests`, which uses `WebApplicationFactory<Program>`) against the Release build artifacts. The pull request shows a single pass/fail signal for integration test outcome.

**Why this priority**: Integration tests are the only layer that verifies the API's HTTP shape, status-code mapping, and request binding end-to-end. Per the constitution (Principle IV), they are a required coverage area. Bundled at P1 with the unit suite because the user listed both explicitly and because the project's "CI must run `dotnet test`" rule already produces this as a free side effect.

**Independent Test**: Open a pull request that introduces a regression visible only at the HTTP boundary (e.g., a controller returning 500 instead of 400 for an invalid payload). The pipeline must mark the pull request as failed with the relevant integration test's failure visible in the log.

**Acceptance Scenarios**:

1. **Given** a pull request where every integration test passes locally, **When** the pipeline runs the integration test step, **Then** all integration suites pass on the Linux runner and the pull request shows a passing status.
2. **Given** a pull request where at least one integration test fails, **When** the pipeline runs the integration test step, **Then** the failing test's name and the relevant HTTP response/status are visible in the pipeline log and the pull request shows a failing status.
3. **Given** the unit test step has failed, **When** the pipeline reaches the integration test step, **Then** the integration test step may proceed independently OR may be short-circuited — both behaviours are acceptable as long as the overall pull request status reflects the unit failure.

---

### Edge Cases

- **Concurrent pushes to the same pull request**: a second push while a pipeline is already running supersedes the in-flight run (the older run is cancelled or its result is ignored) so reviewers always see the freshest signal.
- **Dependency-restore network flake**: a transient network error during dependency restore manifests as a failed pipeline; re-running the pipeline from the pull request UI is sufficient to recover (no permanent state to clean up).
- **Pull requests from forks**: the pipeline must still execute build and tests with no secrets required, because the project has no external dependencies (no database, no cloud services) per the constitution.
- **Pull request targeting a non-default branch**: the pipeline must execute regardless of which protected branch the pull request targets (e.g., `main`, `development`), not just the repository's default branch.
- **Empty pull request (no `.cs` changes, e.g., docs-only)**: the pipeline still runs and still produces a pass/fail signal — there is no "skip CI" path for the MVP.
- **Test that is intermittently flaky**: the pipeline reports the run faithfully (pass or fail as observed); flake remediation is the test author's responsibility, not the pipeline's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pipeline MUST execute automatically when a pull request is opened against a protected base branch, and re-execute on every subsequent push to that pull request's head branch.
- **FR-002**: The pipeline MUST run on a Linux-based runner.
- **FR-003**: The pipeline MUST perform dependency restoration as a discrete first step before building.
- **FR-004**: The pipeline MUST build the solution in **Release** configuration.
- **FR-005**: The pipeline MUST run the project's unit test suites (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`, `Finance.Api.UnitTests`) against the Release build.
- **FR-006**: The pipeline MUST run the project's integration test suite (`Finance.Api.IntegrationTests`) against the Release build.
- **FR-007**: The pipeline MUST surface a single overall pass/fail status to the pull request such that branch-protection rules can require it before merge.
- **FR-008**: The pipeline MUST fail the overall pull request status if **any** of restore, build, unit tests, or integration tests fail.
- **FR-009**: The pipeline MUST NOT require any external service, database, container, or secret to execute — it runs purely against source code and the project's declared package dependencies.
- **FR-010**: The pipeline definition MUST live at `.github/workflows/ci.yml` (the path the constitution already names) so the constitution's CI gate can reference it directly.
- **FR-011**: When a new push arrives for an already-running pull request, the pipeline SHOULD cancel or supersede the older in-flight run so reviewers see the freshest result rather than two stale ones.
- **FR-012**: The pipeline MUST surface, for any failing test, the fully-qualified test name and its failure message in the run log so the author can diagnose without re-running anything locally.

### Key Entities

- **Pipeline run**: One execution of the CI workflow tied to a specific pull request + commit SHA. Carries an overall status (queued / running / success / failure / cancelled) and per-step statuses for restore, build, unit tests, integration tests. Surfaced as a pull request check.
- **Workflow definition file**: A single committed YAML file at `.github/workflows/ci.yml` describing the trigger, the runner, and the ordered steps. The on-disk source of truth for what the pipeline does — versioned with the rest of the code.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a pull request against a protected branch, the pipeline starts within 1 minute of the push that opened or updated the pull request.
- **SC-002**: For a fresh pull request branch with no warm caches, the full pipeline (restore + build + unit tests + integration tests) completes in under 10 minutes.
- **SC-003**: 100% of pull requests that include a broken test, broken build, or broken restore are blocked from merging by a failing required pipeline status, with zero manual reviewer intervention.
- **SC-004**: A reviewer can determine, from the pull request page alone (without opening the pipeline log), whether each of the four steps (restore, build, unit, integration) passed or failed.
- **SC-005**: Pull requests opened from forks complete the same pipeline successfully with no maintainer-supplied secrets, demonstrating zero external-dependency surface.
- **SC-006**: When a pull request receives multiple rapid pushes in succession, only the most recent push's pipeline run reports a final status on the pull request (older in-flight runs are cancelled or superseded).

## Assumptions

- The pull request workflow targets at least the `main` branch; targeting `development` (the current working branch) is also in scope because the project already uses both. Other branches are out of scope unless explicitly listed later.
- "Build + Release" in the user description means a single Release-configuration `dotnet build`; no packaging, no artifact upload, no publish step, no Docker image, and no deployment.
- Unit and integration tests are executed by the same `dotnet test` invocation (the project's four test projects under `src/backend/FinanceTracker/tests/`) — splitting into separate jobs is an optimization, not a requirement.
- The runner is the GitHub-hosted `ubuntu-latest` image; no self-hosted runner, no custom container.
- No code-coverage collection, no SonarCloud, no security scanning, no static analysis beyond what the C# compiler already emits. These are explicitly NOT in scope for this feature.
- No notification side-channels (Slack, email) beyond the standard GitHub UI / email notifications that GitHub Actions provides by default.
- The pipeline reads the .NET SDK version from the repository's `global.json` if present, or from the `net10.0` TargetFramework otherwise. Pinning the SDK version inside the workflow file is acceptable and is the implementer's call.
- NuGet package caching between runs is desirable for speed but optional — its inclusion is a non-functional implementation detail and not a requirement of this spec.
- The pipeline definition is the **only** GitHub Actions workflow file added by this feature. No release pipeline, no scheduled pipeline, no dependabot config.
- Branch-protection rule configuration (marking the workflow's check as "required") is a one-time repository-settings change made by the maintainer outside the workflow file itself; it is acknowledged here but not authored by this feature.
