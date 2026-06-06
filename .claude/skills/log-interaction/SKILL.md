---
name: log-interaction
description: Append a rich structured entry to ai-artifacts/agent_log.txt for every AI interaction
---

# Log Interaction

Append a complete, structured entry to `ai-artifacts/agent_log.txt` documenting the most recent meaningful AI interaction in this session.

## Entry format

Use this block verbatim — no deviations, no extra fields:

```
---
[YYYY-MM-DD HH:MM:SS] | Model: claude-sonnet-4-6
Prompt:     <1–2 sentence summary of what the user asked>
Suggestion: <1–2 sentence summary of what Claude proposed or implemented>
Decision:   accepted | rejected | modified
Reason:     <user's stated motivation, or "n/a" if self-evident>
Files:      <comma-separated relative paths of files changed, or "none">
---
```

## Steps

1. Get the current timestamp. Today is 2026-06-06; estimate HH:MM from context.
2. Write a one-to-two sentence **Prompt** summary — the user's task or request this turn.
3. Write a one-to-two sentence **Suggestion** — what Claude proposed, changed, or built.
4. Set **Decision**: `accepted` (changes were made and user approved), `rejected` (user declined), or `modified` (approach changed before acceptance).
5. Set **Reason**: the user's motivation or constraint, or `n/a`.
6. List **Files**: every file you edited or created this turn (relative to repo root), comma-separated.
7. Append the formatted entry using the PowerShell tool — **do not read the file first**:

```powershell
$entry = @'

---
[2026-06-06 HH:MM:SS] | Model: claude-sonnet-4-6
Prompt:     ...
Suggestion: ...
Decision:   accepted
Reason:     n/a
Files:      ...
---
'@
Add-Content -Path 'ai-artifacts/agent_log.txt' -Value $entry -Encoding utf8
```

## Rules

- `agent_log.txt` is **append-only**. Never read it, truncate it, or rewrite it.
- Call this skill at the end of every turn where files were created or modified.
- If a stub entry was already written by the auto-log hook this turn, the rich entry coexists with it — that is intentional.
