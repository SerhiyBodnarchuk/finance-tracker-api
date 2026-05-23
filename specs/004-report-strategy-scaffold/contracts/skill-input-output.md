# Contract: Skill Input / Output

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

Defines the contract between the developer and the `report-strategy-scaffold` skill: how the skill is invoked, what arguments it consumes, what it produces, and how it refuses.

## Invocation

```
/report-strategy-scaffold <free-text description> [--preview]
```

The skill is also invocable via natural language (e.g., "add a new report type for ISO weeks") because `disable-model-invocation: false` (per data-model.md §1).

### Argument parsing

The skill treats the free-text as the **Report Type Input** described in data-model.md §2. If any required field is missing from the free-text, the skill asks for it conversationally **before any analysis or file writes**. The skill is permitted to ask, at most, the following questions, in order:

1. Report type name (PascalCase identifier).
2. Payload field list (name, type, required).
3. Resolution rule (free-text describing how the payload yields a `[start, end]` date range).
4. Period descriptor format (the literal format string that produces the `period` field in `ReportResult`).
5. Optional payload-specific validation rules.

The `--preview` flag (and `--dry-run` as an alias) sets `IsPreview = true` and short-circuits all file writes.

## Output

### Accept path (no refusal triggers, no preview flag)

The skill emits, in order:

1. A one-paragraph plan summary, listing each path it is about to touch and the FR sub-bullet justifying it.
2. The file edits and creates, per `contracts/file-writes.md`.
3. `dotnet build` and `dotnet test` results, trimmed per `research.md` R-007.
4. The appended `ai-artifacts/agent_log.txt` block, per `contracts/agent-log-entry.md`.
5. A final summary message to the developer naming:
   - The new `ReportType` enum value.
   - Every path created or modified.
   - Build + test verdicts.
   - Any `[Fact(Skip = …)]` tests left for the developer to complete (FR-013).
   - Either "ready to commit" or a follow-up list.

### Preview path (`--preview` set)

The skill emits steps 1 and the planned-write list from step 2 (paths only, no content writes), then exits. **No file writes. No agent-log append. No `dotnet build`/`dotnet test` invocations.**

### Refuse path (any refusal trigger fires)

The skill emits:

1. The refusal code (one of the codes in the table below) and the human-readable explanation, naming the specific FR-014 sub-rule (or constitution principle) violated.
2. An empty list of file changes (verbatim: "Files changed: 0").
3. An appended `ai-artifacts/agent_log.txt` block with `decision: rejected` (per FR-012).
4. No `dotnet build`/`dotnet test` invocations.

## Refusal codes

| Code | FR | Triggered when… | Message template (developer-facing) |
|---|---|---|---|
| `R1-strategy-exists` | FR-014(a) | An `IReportStrategy` implementation is already registered for the requested `ReportType` (per research.md R-001). | "A strategy named `<Name>ReportStrategy` already exists at `<path>`. Remove or rename it before re-running the skill." |
| `R2-persistence-required` | FR-014(b) | The Report Type Input names persistence, caching, or any storage beyond per-request aggregation. | "Constitution principle II forbids persisting or caching reports; the requested behaviour would require a store. Reshape the request to a per-request aggregation, or amend the constitution first." |
| `R3-multi-range-payload` | FR-014(c) | The Report Type Input's `RangeRule` cannot be reduced to a single inclusive `[start, end]` per request (e.g., diff between two windows; arbitrary union of windows). | "The strategy contract resolves a request to exactly one inclusive date range; the supplied rule describes <N> ranges. Split the request, or amend the constitution first." |
| `R4-missing-anchor` | FR-014(d) | The anchor file `src/backend/FinanceTracker/Finance.Business/Services/Reports/PeriodReportStrategy.cs` is not present at the resolved path. | "I can't find `Finance.Business/Services/Reports/PeriodReportStrategy.cs`. Are you running this skill from the wrong directory or on a checkout that predates feature 003?" |
| `R5-name-not-normalisable` | FR-014 (via Edge Case 1) | The supplied name cannot be normalised to a valid PascalCase C# identifier (e.g., name is empty, all digits, or all punctuation). | "I can't normalise '<input>' to a valid C# identifier. Provide a name starting with a letter, using letters/digits only." |
| `R6-dirty-tree-declined` | FR-016 (via Edge Case 2) | The working tree is dirty and the developer declined to proceed when asked. | "Working tree has uncommitted changes and you chose not to proceed. Commit or stash first, then re-run." |

Each refusal code MUST appear in the `suggestion` field of the agent-log entry (per `contracts/agent-log-entry.md`).

## Conversation contract

The skill MAY ask the developer for missing input fields (per Invocation §Argument parsing) and for confirmation on a dirty tree (per FR-016). It MUST NOT ask the developer to confirm a refusal — refusals are decisive. It MUST NOT ask the developer to confirm an accept path either; the accept path proceeds straight to writes once analysis completes.

## Exit semantics

The skill is a Claude Code interaction, so there is no process exit code in the OS sense. The contract's "exit" is the final message to the developer:

- Accept path: ends with the summary message described in Output §Accept.
- Refuse path: ends with the refusal code + explanation.
- Preview path: ends with the planned-writes list.

In all three cases, the next slash command the developer types in the same Claude Code session is unconstrained by this skill's run.
