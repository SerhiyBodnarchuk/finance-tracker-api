# Phase 1 — Data Model: CI Pipeline for Pull Requests

**Feature**: `005-ci-pipeline`
**Plan**: [plan.md](plan.md)
**Date**: 2026-05-24

This feature has **no domain entities** — it adds a build-infrastructure file, not application data. The "data model" relevant to the feature is the **workflow-run state** that GitHub Actions reports back to the pull request UI. This document captures that shape so the spec's Success Criteria (especially SC-004, "reviewers can determine per-step status from the PR page alone") are traceable to an actual data structure.

The workflow-run model below is **owned by GitHub Actions**, not by this repository — but the names and fields it produces are part of the contract the workflow YAML must adhere to. Treat this document as reference, not as a source-of-truth schema.

---

## Entity 1 — `Workflow Run`

One execution of `.github/workflows/ci.yml` for a specific pull request + commit SHA. Identified by a numeric `run_id` that GitHub assigns.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `run_id` | int64 | GitHub | Assigned at queue time; immutable. |
| `workflow_path` | string | YAML | Always `.github/workflows/ci.yml`. |
| `event` | string | trigger | Always `pull_request` (per D3). |
| `pull_request_number` | int | event payload | The PR this run is gating. |
| `head_sha` | string (40-char) | event payload | The commit being tested. |
| `head_ref` | string | event payload | The PR's head branch (`refs/heads/<branch>`). |
| `base_ref` | string | event payload | The PR's target branch (`refs/heads/main` or `refs/heads/development`). |
| `status` | enum | runtime | `queued` → `in_progress` → `completed` (cancellation transitions through `in_progress` → `completed` with `conclusion=cancelled`). |
| `conclusion` | enum | runtime | `success` \| `failure` \| `cancelled` \| `timed_out` \| `action_required` \| `skipped` (only meaningful when `status=completed`). |
| `started_at` | timestamp | runtime | First worker pickup. Used for SC-001 (start-latency ≤ 1 min). |
| `completed_at` | timestamp | runtime | Last step finishes. Used for SC-002 (cold-cache run ≤ 10 min). |
| `concurrency_group` | string | YAML | `"ci-${{ github.workflow }}-${{ github.ref }}"` per D4. |

### State transitions

```text
queued ──(runner accepts)──> in_progress ──(all steps pass)──> completed[success]
queued ──(runner accepts)──> in_progress ──(any step fails)──> completed[failure]
queued ──(newer push)──────> completed[cancelled]                 (cancelled before any step runs)
in_progress ──(newer push)─> completed[cancelled]                 (per D4: cancel-in-progress: true)
in_progress ──(timeout)────> completed[timed_out]                 (defensive — not expected for a 10-min job)
```

### Invariants

- Two `Workflow Run`s with the same `concurrency_group` and overlapping `[started_at, completed_at]` MUST NOT both end in `conclusion=success`. The older one MUST be `cancelled`. (Enforced by `cancel-in-progress: true`.)
- `conclusion=success` is **only** possible when **every** step in Entity 2 succeeded.
- A run for a PR head that is no longer the PR's head (because of force-push or rebase) MAY exist as a `cancelled` run but MUST NOT report `success` against the PR.

---

## Entity 2 — `Workflow Step`

Each `Workflow Run` contains an ordered list of steps. Each step has its own status that surfaces on the PR's checks panel under the parent check.

| Step name | Action / command | Purpose | Failure consequence |
|-----------|------------------|---------|---------------------|
| **Checkout** | `actions/checkout@v4` | Pulls the PR's merge ref into the runner workspace. | Run fails before any code is read. Almost always a GitHub-side problem. |
| **Setup .NET 10** | `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` | Installs the .NET 10 SDK on the runner. | Run fails before restore. Usually means GitHub's package mirror is having a moment. |
| **Restore NuGet cache** | `actions/cache@v4` | Restores `~/.nuget/packages` from a prior cache hit (or notes a miss). | Never fails the run (cache misses are silent). Logged for visibility. |
| **Restore solution** | `dotnet restore FinanceTracker.slnx` | Resolves the package graph. | Run fails. Maps to "broken restore" in SC-003. |
| **Build (Release)** | `dotnet build FinanceTracker.slnx --no-restore -c Release` | Compiles in Release. | Run fails. Maps to "broken build" in SC-003 and to User Story 1 acceptance scenario 2. |
| **Test (Release)** | `dotnet test FinanceTracker.slnx --no-build -c Release` | Runs all four test projects via the VSTest path (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`). | Run fails. Maps to "broken test" in SC-003 and to User Stories 2 and 3. |

### Invariants

- The six steps execute strictly in the order listed. Earlier failure short-circuits later steps (default `if` behaviour — no `if: always()` is used).
- Step names in the YAML MUST match the **Step name** column verbatim, because reviewers rely on per-step status visibility (SC-004) and the names are the only label they see.
- Test failures inside the **Test (Release)** step produce a step-level `failure` conclusion *and* a console log containing each failing test's fully-qualified name (FR-012). The `xunit.runner.visualstudio` adapter prints this format; no extra logger argument is required (D12).

### Step → User-Story / FR mapping

| Step | User stories | Functional requirements |
|------|--------------|-------------------------|
| Checkout | (precondition for all) | — |
| Setup .NET 10 | (precondition for build/test) | FR-002 (Linux runner needs an SDK) |
| Restore NuGet cache | (performance, not correctness) | — (supports SC-002) |
| Restore solution | US1 acceptance scenario 3 | FR-003, FR-008 |
| Build (Release) | US1 | FR-004, FR-008 |
| Test (Release) | US2, US3 | FR-005, FR-006, FR-008, FR-012 |

---

## Entity 3 — `Check Run` (the PR-visible signal)

GitHub creates one **Check Run** per Workflow Run, named after the workflow (`CI`, derived from the YAML's `name:` field). This is what the reviewer sees on the PR page.

| Field | Source |
|-------|--------|
| `name` | YAML `name:` → `"CI"` |
| `head_sha` | event payload (matches the Workflow Run's `head_sha`) |
| `status` | Workflow Run `status` |
| `conclusion` | Workflow Run `conclusion` |
| `details_url` | Link to the Workflow Run log |
| `required` | Repository-side branch protection rule (NOT controlled by this YAML) |

### Branch-protection contract

For SC-003 ("100% of broken PRs blocked from merging") to hold, the repository administrator MUST mark this Check Run as **Required** in the branch-protection rule for both `main` and `development`. The YAML cannot enforce this — it can only produce the check. This is the maintainer responsibility noted in the spec's Assumptions section.

---

## What is intentionally NOT modelled

- **Test results as structured data** — no TRX upload, no JUnit XML, no Sonar report. Console log only (D12). If a future feature needs structured data, it can add `--logger trx` and `actions/upload-artifact` without redesigning anything in this document.
- **Build artifacts** — no `.dll` upload, no NuGet package publish. The Release build's outputs are discarded at the end of the run.
- **Test coverage** — out of scope per the spec.
- **Performance metrics** — start latency and run duration are observable from the GitHub UI; we do not push them to any external telemetry.
