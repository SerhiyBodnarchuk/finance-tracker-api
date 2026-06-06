---
name: git-ship
description: Stage uncommitted changes, create a commit, push the current branch to GitHub, and open a pull request targeting the development branch.
argument-hint: "Optional commit message or PR title override (e.g. 'feat: add export endpoint')"
user-invocable: true
---

# Git Ship

Stage, commit, push, and open a PR to `development` — all in one step.

## User Input

```text
$ARGUMENTS
```

If the user supplied a non-empty argument treat it as the **commit message** (and use it also as the PR title). If empty, use the branch name to form the message: `Changes for <branch-name>` (e.g. `Changes for 007-mcp-server`).

## Steps

### 1 — Safety checks

Run the following and abort (with a clear message) if any check fails:

```powershell
git rev-parse --is-inside-work-tree
```

- Abort if not inside a git repository.
- Read the current branch name:
  ```powershell
  git rev-parse --abbrev-ref HEAD
  ```
- Abort if the current branch is `main`, `master`, or `development` — shipping directly to those branches is not allowed. Ask the user to switch to a feature branch first.

### 2 — Check for changes

```powershell
git status --porcelain
```

- If the output is empty there is nothing to commit. Report "Nothing to commit — working tree clean." and stop.

### 3 — Stage changes

Add all modified and new tracked/untracked files **except** known secrets and generated artefacts:

```powershell
git add --all
```

Then explicitly un-stage files that must never be committed (if any slipped in):

```powershell
git restore --staged .env
git restore --staged .env.local
git restore --staged "*.pfx"
git restore --staged "*.p12"
git restore --staged "*.key"
```

If any of those un-stage commands fail because the file wasn't staged, ignore the error and continue.

### 5 — Draft the commit message

If the user supplied a message in `$ARGUMENTS`, use it verbatim.

Otherwise use: `Changes for <branch-name>` (e.g. `Changes for 007-mcp-server`).

Always append the trailer:

```
Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

### 6 — Commit

Use a PowerShell here-string:

```powershell
$msg = @'
Changes for <branch-name>

Co-Authored-By: Claude <noreply@anthropic.com>
'@
git commit -m $msg
```

### 7 — Push

```powershell
git push --set-upstream origin (git rev-parse --abbrev-ref HEAD)
```

If the push is rejected because the remote branch has diverged, report the error and stop — do **not** force-push without explicit user instruction.

### 8 — Draft the PR

Compose the PR title and body:

- **Title**: first line of the commit message (strip the trailer).
- **Body**:

```markdown
## Summary
<2–4 bullet points derived from the commit message and branch name>

## Test plan
- [ ] Build passes (`dotnet build`)
- [ ] All tests pass (`dotnet test`)
- [ ] Manual smoke-test of changed endpoints

```

Show the title and body to the user and ask for confirmation before creating the PR.

### 9 — Create the PR

Use `gh pr create` (preferred) or the `mcp__github__create_pull_request` MCP tool:

**gh CLI approach:**

```powershell
gh pr create `
  --base development `
  --title "<PR title>" `
  --body @'
<PR body>
'@
```

**MCP fallback** (if `gh` is unavailable): call `mcp__github__create_pull_request` with:
- `owner`: `SerhiyBodnarchuk`
- `repo`: `finance-tracker-api`
- `title`: PR title
- `body`: PR body
- `head`: current branch name
- `base`: `development`

### 10 — Report

Print a concise summary:

```
✓ Committed: <short hash> <commit message first line>
✓ Pushed:    origin/<branch>
✓ PR opened: <PR URL>
```

## Error handling

| Situation | Action |
|-----------|--------|
| On `main`/`master`/`development` | Abort with message, do nothing |
| Nothing to commit | Report and stop |
| Push rejected (non-fast-forward) | Report error, do not force-push |
| `gh` not installed and MCP unavailable | Report and stop after push |
| PR already exists for this branch | Report the existing PR URL and stop |

## Notes

- Never use `--no-verify` or `--force` without explicit user instruction.
- Never commit `.env`, credential files, or binary secrets.
- The target base branch is always `development`, not `main`.
