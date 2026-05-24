# Quickstart — Verifying the CI Pipeline

**Feature**: `005-ci-pipeline`
**Plan**: [plan.md](plan.md)
**Date**: 2026-05-24

This document is the manual-validation walkthrough for the CI pipeline. The workflow file itself has no unit tests (D-Testing in plan.md); its correctness is verified by running it against deliberately-constructed pull requests and observing GitHub Actions' behaviour.

Use this checklist after `/speckit-implement` writes `.github/workflows/ci.yml`, before marking the feature done.

---

## Prerequisites

1. The branch `005-ci-pipeline` has the workflow file at `.github/workflows/ci.yml`.
2. The branch is pushed to `origin`.
3. You have permission to open pull requests against `main` and/or `development`.

---

## V1 — Smoke test: green PR

**Goal**: prove the happy path works on a clean PR.

1. Open a PR from `005-ci-pipeline` (or any descendant branch with the workflow file) targeting `development`.
2. Watch the **Checks** tab on the PR page. Within ~1 minute, the `CI` check should appear in `Queued` / `In progress` state (SC-001).
3. Wait for completion. On a cold cache, expect ≤ 10 minutes (SC-002); warmly, ≤ 4 minutes (informational).
4. **Expected**: `CI` shows a green check. All six step names from [contracts/workflow-contract.md](contracts/workflow-contract.md) C6 are visible in the checks panel, each green (SC-004).

**Pass criteria**:
- Overall `conclusion: success`.
- Each step shows its name (Checkout, Setup .NET 10, Restore NuGet cache, Restore solution, Build (Release), Test (Release)) and a green tick.
- `Restore NuGet cache` shows `Cache miss` on the **first** run (no prior cache exists for the branch's csproj hash) and `Cache hit` on the second run after no csproj change.

---

## V2 — Red PR: deliberate compile error

**Goal**: prove the build step fails when source code doesn't compile (User Story 1, AC2).

1. From a child of the same branch, introduce a compile error: e.g., add `garbage_text_here;` at the top of `Finance.Api/Program.cs`. Commit and push.
2. Either open a new PR targeting `development`, or push to the existing PR's head branch.

**Pass criteria**:
- `CI` check shows red.
- The **Build (Release)** step shows red; **Restore solution** is green; **Test (Release)** did NOT execute (it's blue / not started, not red).
- The compile error's file + line is visible in the log of the **Build (Release)** step.

3. Revert the deliberate breakage. Push.
4. The check turns green within the next run.

---

## V3 — Red PR: deliberate test failure

**Goal**: prove the test step fails when an assertion breaks (User Story 2 / User Story 3, AC2).

1. From a child branch, edit any existing test — e.g., change an `Assert.Equal(2, 1+1)` to `Assert.Equal(3, 1+1)`. Commit and push to a PR head.

**Pass criteria**:
- `CI` check shows red.
- **Build (Release)** is green; **Test (Release)** is red.
- The log under **Test (Release)** lists the failing test's fully-qualified name and the assertion message (FR-012).

2. Revert. Push.
3. Check turns green.

---

## V4 — Supersede on rapid push

**Goal**: prove `concurrency: cancel-in-progress: true` cancels the older run (SC-006, FR-011).

1. Push commit `A` to a PR head. Watch the `CI` check start.
2. Within ~30 seconds (while the workflow is still in `Restore` or `Build`), push commit `B` to the same head.
3. Observe the Actions tab on the repository.

**Pass criteria**:
- Two runs are listed; the older one (for commit `A`) shows `conclusion: cancelled`.
- The newer one (for commit `B`) runs to completion and reports its status on the PR.
- Only one Check Run line is "current" on the PR page (the one for commit `B`).

---

## V5 — Fork-PR equivalence (optional)

**Goal**: prove no secrets are required (SC-005, FR-009).

This step requires a second account or a colleague with fork permissions. Skip if not practical.

1. From a fork of the repository, open a PR back to this repository's `development`.
2. The PR's `CI` check should run identically — same six steps, same outcome.

**Pass criteria**:
- The run completes (success or failure on its own merits).
- No step references `secrets.*` and no step fails with an "access denied" / "permission" error.

---

## V6 — Branch-protection wiring (one-time, post-implementation)

**Goal**: actually realize SC-003 ("100% blocked from merging").

This is a repository-settings change, not a YAML change. Document it here so it doesn't get forgotten when the workflow lands.

1. After at least one successful run of `CI` exists in the repository, go to **Settings → Branches → Branch protection rules**.
2. Add (or edit) a rule covering `main` and `development`.
3. Under **Require status checks to pass before merging**, search for `CI` and add it to the required-checks list.
4. Save.

**Pass criteria**:
- The PR page now shows "Required" next to the `CI` check.
- The Merge button is disabled when `CI` is red or pending.
- The Merge button enables once `CI` is green.

---

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Workflow doesn't appear on the PR at all | Trigger filter excludes the base branch | Re-read [contracts/workflow-contract.md](contracts/workflow-contract.md) C2; confirm the base branch is `main` or `development`. |
| `Setup .NET 10` step fails with "no matching version" | `10.0.x` not available on this runner image yet | Pin to a specific patch (e.g. `'10.0.100'`) only as a hotfix; open an amendment to D2 if this becomes a recurring issue. |
| `Restore solution` fails with "package not found" | NuGet feed flake or a private feed not configured | Re-run from the PR. If persistent, check whether any `.csproj` added a private feed; this MVP only supports nuget.org. |
| `Restore NuGet cache` always reports `Cache miss` | Cache key changed (someone edited a `.csproj`) — expected | No action; the cache will warm up on the next identical key. |
| Pipeline takes longer than 10 minutes | Cold cache + slow runner, or a hung test | Open the log of the longest step. If a test hung, that test has a defect; not a pipeline defect. |
| Two green checks for the same PR after rapid push | `cancel-in-progress` typo'd or missing | Re-check [contracts/workflow-contract.md](contracts/workflow-contract.md) C4. |
