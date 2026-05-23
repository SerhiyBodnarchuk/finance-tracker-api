---

description: "Task list for feature 004 — `report-strategy-scaffold` Claude skill"
---

# Tasks: `report-strategy-scaffold` Claude Skill

**Input**: Design documents from `specs/004-report-strategy-scaffold/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Tests are intentionally light for this feature. The deliverable is a single `SKILL.md` prompt — not executable code — so traditional unit/integration tests do not apply. Verification is **manual against `quickstart.md`** plus a contract self-check that the emitted file set matches `contracts/file-writes.md`. There are no automated tests of the skill itself in this feature; the skill's *output* is exercised by the existing `Finance.*` test projects whenever a developer runs the skill against a real report-type request.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently. Because almost all production work happens inside one file (`.claude/skills/report-strategy-scaffold/SKILL.md`), [P] markers are sparse — most tasks touch the same file and must run sequentially.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies).
- **[Story]**: Which user story the task belongs to (US1, US2, US3). Setup, Foundational, and Polish tasks have no story label.
- File paths in task descriptions are absolute against the repo root.

## Path Conventions

This feature edits exactly two paths in the repo (plus one append):

- `.claude/skills/report-strategy-scaffold/SKILL.md` — created in Phase 1, written across Phases 2–5.
- `CLAUDE.md` — already updated in `/speckit-plan` to point at this plan; this feature does not touch it further.
- `ai-artifacts/agent_log.txt` — appended once at Polish (T026) for the feature itself, per constitution Principle V.

Nothing in `src/backend/FinanceTracker/` is modified by `/speckit-implement` of this feature. The skill's *output* edits the source tree — but that happens at skill invocation time, not feature implementation time.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the skill folder and the empty `SKILL.md` skeleton.

- [X] T001 Create the directory `.claude/skills/report-strategy-scaffold/` at the repo root.
- [X] T002 Create the file `.claude/skills/report-strategy-scaffold/SKILL.md` with YAML frontmatter exactly matching [data-model.md §1](data-model.md) (`name`, `description`, `argument-hint`, `compatibility`, `metadata.author`, `metadata.source`, `user-invocable: true`, `disable-model-invocation: false`). Body below the frontmatter should be a single H1 `# report-strategy-scaffold` followed by an `## Overview` paragraph stating the skill's purpose and pointing back at [specs/004-report-strategy-scaffold/spec.md](spec.md) and [plan.md](plan.md).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Author the shared sections of `SKILL.md` that every user story depends on — input parsing, repo-state analysis, the agent-log helper, and dirty-tree handling. No user-story phase can be tested until these are in place.

**⚠️ CRITICAL**: No user-story phase can begin until Phase 2 is complete. Every section below is appended to the same `SKILL.md` file in order.

- [X] T003 In `.claude/skills/report-strategy-scaffold/SKILL.md`, append a `## Input` section that:
  - Documents the slash-command invocation (`/report-strategy-scaffold <free-text> [--preview]`).
  - Lists the five conversational prompts the skill MAY ask in order (Name, PayloadFields, RangeRule, PeriodDescriptorFormat, ValidationRules) per [contracts/skill-input-output.md §Argument parsing](contracts/skill-input-output.md).
  - Specifies that prompts MUST come **before** any file analysis or file write.
- [X] T004 In the same file, append a `## Analysis` section that instructs the model to:
  - Read `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs` and derive `EnumValueAlreadyExists` ([data-model.md §2](data-model.md)).
  - Scan `src/backend/FinanceTracker/Finance.Business/Services/Reports/*ReportStrategy.cs` for any class with `Type => ReportType.<Name>`; derive `StrategyAlreadyExists` (per [research.md R-001](research.md)).
  - Verify the anchor file `src/backend/FinanceTracker/Finance.Business/Services/Reports/PeriodReportStrategy.cs` is present.
  - Classify the input as Accept / Refuse / Preview **before any Write or Edit tool call** (per [research.md R-005](research.md)).
- [X] T005 In the same file, append an `## Agent log entry` section reproducing the 8-field block format from [contracts/agent-log-entry.md](contracts/agent-log-entry.md), including the three worked examples (accepted, rejected, needs-revision), and stating that the file is `ai-artifacts/agent_log.txt` and the operation is always append-only.
- [X] T006 In the same file, append a `## Dirty-tree handling` section that:
  - Specifies the single allowed git read: `git status --porcelain` ([research.md R-006](research.md)).
  - Defines the warn-and-confirm flow when the tree is dirty.
  - Forbids all state-changing `git` commands (per [contracts/file-writes.md §Accept-path allow-list](contracts/file-writes.md) explicit exclusions).

**Checkpoint**: SKILL.md now has frontmatter + Overview + Input + Analysis + Agent log entry + Dirty-tree handling. The skill can collect input and analyse repo state but cannot yet write any scaffold or refusal.

---

## Phase 3: User Story 1 — Scaffold a new report type end-to-end (Priority: P1) 🎯 MVP

**Goal**: When the developer supplies a valid Report Type Input and no refusal trigger fires, the skill produces the eight-file artifact set ([data-model.md §3](data-model.md) / [contracts/file-writes.md §Accept-path allow-list](contracts/file-writes.md)), runs `dotnet build` + `dotnet test`, appends the accept-path agent-log entry, and reports a final summary.

**Independent Test**: With a clean working tree on this branch, invoke `/report-strategy-scaffold` with the IsoWeek description from [quickstart.md §Invocation](quickstart.md). Confirm:

1. The eight paths in `contracts/file-writes.md` are created/edited (six modified file paths plus the `ai-artifacts/agent_log.txt` append; the enum row is skipped because `IsoWeek` is already declared).
2. `dotnet build` and `dotnet test` from `src/backend/FinanceTracker` both exit `0`.
3. `POST /api/reports` with `{ "type": "IsoWeek", "data": { "week": "2026-W19" } }` returns HTTP 200 with `type: "IsoWeek"`.
4. `POST /api/reports` with the canonical Period payload returns the byte-identical response from before the skill ran (SC-006).
5. `ai-artifacts/agent_log.txt` has a new accept-path entry matching [contracts/agent-log-entry.md §Worked examples Example 1](contracts/agent-log-entry.md).

### Implementation for User Story 1

Each task below appends one section to `.claude/skills/report-strategy-scaffold/SKILL.md`. Sections are written in this order so the final file reads top-to-bottom in execution order. No [P] markers because every task touches the same file.

- [X] T007 [US1] In SKILL.md, append an `## Accept path — Step 1: enum value` section instructing the model to edit `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs` per [contracts/file-writes.md §Row 1 / §Diff-allowlist rules / Row 1](contracts/file-writes.md). Specify the skip condition when `EnumValueAlreadyExists` is `true`.
- [X] T008 [US1] In SKILL.md, append an `## Accept path — Step 2: data DTO` section instructing the model to create `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/<Name>ReportData.cs` per [contracts/file-writes.md §Row 2](contracts/file-writes.md). Include the C# style requirements from [research.md R-008](research.md) (file-scoped namespace `Finance.Business.Dtos.Reports`, `public sealed record` with positional parameters, no XML doc comments).
- [X] T009 [US1] In SKILL.md, append an `## Accept path — Step 3: strategy class` section instructing the model to create `src/backend/FinanceTracker/Finance.Business/Services/Reports/<Name>ReportStrategy.cs` mirroring the five-step structure of `PeriodReportStrategy.cs` (parse → validate → resolve-to-range → filter → aggregate → sort). Include the TODO-marker + `[Fact(Skip = …)]` rule from [research.md R-004](research.md) for non-trivial `RangeRule` cases.
- [X] T010 [US1] In SKILL.md, append an `## Accept path — Step 4: DI registration` section instructing the model to insert exactly one line `builder.Services.AddSingleton<IReportStrategy, <Name>ReportStrategy>();` into `src/backend/FinanceTracker/Finance.Api/Program.cs` immediately after the existing `PeriodReportStrategy` registration (currently at line 30) per [research.md R-002](research.md) and [contracts/file-writes.md §Row 4 / §Diff-allowlist rules / Row 4](contracts/file-writes.md). Explicitly enumerate the disallowed modifications.
- [X] T011 [US1] In SKILL.md, append an `## Accept path — Step 5: strategy unit tests` section instructing the model to create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/<Name>ReportStrategyTests.cs` mirroring `PeriodReportStrategyTests.cs` per [research.md R-009](research.md). Require xUnit v3 + `Xunit.Assert` + Moq 4.20.x strict-mode mocks; forbid FluentAssertions. Emit at minimum: happy-path aggregation; invalid payload → `ReportValidationException`; payload-shape edge cases specific to the new type; `period` descriptor formatting.
- [X] T012 [US1] In SKILL.md, append an `## Accept path — Step 6: factory test extension` section instructing the model to append one `[Fact]` to `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs` per [research.md R-010](research.md) and [contracts/file-writes.md §Row 6 / §Diff-allowlist rules / Row 6](contracts/file-writes.md). The new test asserts `factory.TryGet(ReportType.<Name>)` returns a non-null `IReportStrategy` whose `Type` equals the new enum value. Existing tests untouched.
- [X] T013 [US1] In SKILL.md, append an `## Accept path — Step 7: integration test extension` section instructing the model to append one `[Fact]` to `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs` per [contracts/file-writes.md §Row 7 / §Diff-allowlist rules / Row 7](contracts/file-writes.md). The new test uses the existing `ApiTestFixture` to post `{ "type": "<Name>", "data": { ... } }` against the seeded data and asserts HTTP 200 plus `type == "<Name>"` in the body.
- [X] T014 [US1] In SKILL.md, append an `## Accept path — Step 8: build + test verification` section instructing the model to (a) run `dotnet build` from `src/backend/FinanceTracker/`, (b) run `dotnet test` from the same directory, (c) trim the output per [research.md R-007](research.md) (first matched `error CS\d+:` / `error MSB\d+:` for build; first matched `Failed `/`Test Run Failed` for tests, plus 3 lines of context). Reject "print full log" as an option.
- [X] T015 [US1] In SKILL.md, append an `## Accept path — Step 9: agent log append` section that uses the format from T005 to compose one entry whose `decision` field is `accepted` (when build + tests are green, with optional `(N skipped)` suffix per R-004) or `needs-revision` (when either fails). Files list is populated from the actual writes from Steps 1–7. Refer to [contracts/agent-log-entry.md §Worked examples Examples 1 and 3](contracts/agent-log-entry.md).
- [X] T016 [US1] In SKILL.md, append an `## Accept path — Step 10: final summary` section instructing the model to emit the developer-facing summary required by FR-013: the new `ReportType` enum value, every path created or modified, build + test verdicts, the names of any `[Fact(Skip = …)]` tests left for the developer, and the "ready to commit" / follow-up list. Match the transcript in [quickstart.md §Expected interaction transcript](quickstart.md).
- [ ] T017 [US1] **Manual verification — DEFERRED (fresh session required)** — On a clean working tree, invoke `/report-strategy-scaffold` with the IsoWeek description from [quickstart.md §Invocation](quickstart.md). Confirm all six items in the Independent Test section above. After confirming, run `git restore .` so the verification scaffold doesn't get committed with the feature.

**Checkpoint**: User Story 1 fully working. A developer can scaffold a new report type end-to-end with one invocation. The skill cannot yet refuse cleanly (Phase 4) or preview (Phase 5).

---

## Phase 4: User Story 2 — Refuse to scaffold when the request conflicts or is unsupported (Priority: P2)

**Goal**: Before any file write, the skill detects the four refusal triggers from FR-014 (plus the two normalisation/dirty-tree triggers in `contracts/skill-input-output.md`), emits the matching refusal code, and exits with the working tree byte-identical apart from one append to `ai-artifacts/agent_log.txt`.

**Independent Test**: From a clean working tree, run the three refusal scenarios from [spec.md §User Story 2 §Independent Test](spec.md):

1. `/report-strategy-scaffold Period` → exits with `R1-strategy-exists`. `git status --porcelain` shows only `ai-artifacts/agent_log.txt`.
2. `/report-strategy-scaffold a monthly diff that compares two months and persists the result` → exits with `R2-persistence-required` OR `R3-multi-range-payload` (either is correct; the developer can grep for the right code in the message).
3. Move to a sibling directory without the anchor file and re-invoke → exits with `R4-missing-anchor`.

For all three, the agent-log entry's `decision` field is `rejected`. No accept-path side effects.

### Implementation for User Story 2

All tasks edit `.claude/skills/report-strategy-scaffold/SKILL.md` and run sequentially.

- [X] T018 [US2] In SKILL.md, append a `## Refuse path — refusal codes` section reproducing the six-row table from [contracts/skill-input-output.md §Refusal codes](contracts/skill-input-output.md) (R1-strategy-exists, R2-persistence-required, R3-multi-range-payload, R4-missing-anchor, R5-name-not-normalisable, R6-dirty-tree-declined). For each, specify the detection rule the model applies during the Analysis step (T004).
- [X] T019 [US2] In SKILL.md, append a `## Refuse path — final message` section instructing the model to emit the refusal code, the human-readable explanation, the verbatim string `Files changed: 0`, and an "Appended ai-artifacts/agent_log.txt entry [decision: rejected]" line. The message MUST NOT invite the developer to confirm or override.
- [X] T020 [US2] In SKILL.md, append a `## Refuse path — agent log append` section that composes one entry whose `decision` is `rejected`, `suggestion` is `Refuse: <code> — <one-clause rationale>`, `files` is `0`, and both `build` and `tests` are `unknown`. Refer to [contracts/agent-log-entry.md §Worked examples Example 2](contracts/agent-log-entry.md). Also state the refuse-path no-op invariant from [contracts/file-writes.md §Refuse-path no-op guarantee](contracts/file-writes.md): after the run, only `ai-artifacts/agent_log.txt` may differ from the pre-invocation state.
- [ ] T021 [US2] **Manual verification — DEFERRED (fresh session required)** — From a clean working tree, run the three refusal scenarios from the Independent Test section above. For each, verify:
  - The refusal code printed matches the expected one.
  - `git diff --stat` after the invocation shows only `ai-artifacts/agent_log.txt` and nothing else.
  - `git status --porcelain --untracked-files=all` shows zero untracked new files.
  - The appended log entry's `decision` field is `rejected`.

**Checkpoint**: User Stories 1 AND 2 both work independently. Accept and refuse paths are covered.

---

## Phase 5: User Story 3 — Preview the changes before writing (Priority: P3)

**Goal**: When `--preview` (or `--dry-run`) appears in the input, the skill performs the same analysis as a real accept-path run but writes nothing — no file mutations, no agent-log append, no `dotnet build`/`dotnet test` invocations.

**Independent Test**: From a clean working tree, invoke `/report-strategy-scaffold --preview <IsoWeek description>`. After the run:

1. `git status --porcelain --untracked-files=all` is empty.
2. The skill printed the same list of file paths that a real accept-path run would have produced (compare side-by-side against the T017 verification output).
3. Re-invoking without `--preview` with identical input produces the file set the preview listed (paths must match exactly).

### Implementation for User Story 3

- [X] T022 [US3] In SKILL.md, append a `## Preview path — flag detection` section instructing the model to parse `--preview` and `--dry-run` (alias) from the invocation arguments and set `IsPreview = true`. Specify that the flag MUST be detected during the Input step (T003) so that the Analysis step (T004) knows whether to suppress the writes.
- [X] T023 [US3] In SKILL.md, append a `## Preview path — output and no-op guarantee` section instructing the model to:
  - Print the same plan summary as the accept path (T016's content) but with no actual writes.
  - Emit a closing line `No files written. No agent-log entry.` per the transcript in [quickstart.md §Preview demo](quickstart.md).
  - Not invoke `dotnet build` or `dotnet test` (per [contracts/skill-input-output.md §Output / Preview path](contracts/skill-input-output.md)).
  - Not append to `ai-artifacts/agent_log.txt` (FR-017).
- [ ] T024 [US3] **Manual verification — DEFERRED (fresh session required)** — Run the three checks from this phase's Independent Test section. After confirming, ensure no follow-up `git restore` is needed (a successful preview leaves the tree untouched by definition).

**Checkpoint**: All three user stories complete. The skill is feature-complete against spec.md.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, agent-log entry for the *feature creation itself*, and contract self-check.

- [X] T025 [P] Sanity-check that the skill appears in Claude Code's `/`-completion list: open a fresh Claude Code session, type `/report-`, and confirm `report-strategy-scaffold` is listed with the `description` from `SKILL.md` frontmatter. If not, the frontmatter is malformed — fix and re-verify before continuing.
- [ ] T026 [P] **DEFERRED (fresh session required)** — Run the **full quickstart.md scenario end-to-end** (IsoWeek accept → refuse → preview) as the final acceptance gate for SC-001..SC-006. Treat any deviation from the expected transcripts in `quickstart.md` as a regression and patch SKILL.md accordingly.
- [X] T027 [P] **Contract self-check**: for one accept-path scaffold (e.g., the IsoWeek demo from T026), diff the actual file set the skill modified against the eight-row allow-list in [contracts/file-writes.md §Accept-path allow-list](contracts/file-writes.md). Any file outside the allow-list — even a stray blank line — is a contract violation; patch SKILL.md until the diff matches exactly.
- [X] T028 Append one entry to `ai-artifacts/agent_log.txt` recording the creation of the `report-strategy-scaffold` skill itself (per constitution Principle V — "AI-Assisted Development Transparency"). Fields: timestamp now, tool `Claude Code (skill: speckit-implement)`, prompt = the original `/speckit-implement` user request, suggestion = `Create .claude/skills/report-strategy-scaffold/SKILL.md per specs/004-report-strategy-scaffold/plan.md`, decision = `accepted`, reason = "Feature 004 implemented and verified per quickstart.md", files = `.claude/skills/report-strategy-scaffold/SKILL.md`, build = `unknown` (no .NET code added), tests = `unknown` (no tests added).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. Blocks all user-story phases.
- **Phase 3 (US1)**: Depends on Phase 2. **MVP increment** — once T017 passes, the skill delivers its core value.
- **Phase 4 (US2)**: Depends on Phase 2 (uses the same Analysis section authored in T004). Independent of Phase 3 in principle, but a sensible implementer finishes Phase 3 first so Analysis already exists in the file.
- **Phase 5 (US3)**: Depends on Phase 2 and benefits from Phase 3 (the preview output reuses the accept-path plan summary from T016). Practically: do after Phase 3.
- **Phase 6 (Polish)**: Depends on all prior phases.

### User Story Dependencies

- **US1**: Independent of US2 and US3. Can ship as the MVP.
- **US2**: Independent of US3. Reads from the same Analysis section as US1 but does not require US1 writes.
- **US3**: Independent of US2. Most cleanly implemented after US1 so the preview output can mirror the accept-path summary, but logically independent.

### Within Each User Story

- Tasks within a user-story phase touch the same SKILL.md file and therefore run sequentially in their listed order.
- The Manual Verification task at the end of each story phase is the gate to the next phase.

### Parallel Opportunities

- T025, T026, T027 in Phase 6 are all read-only verifications of the finished skill — they can run in parallel ([P] marked).
- Within Phases 2–5, **no** tasks are parallelizable because they all edit the single `SKILL.md` file. This is a deliberate consequence of the deliverable being one file. There are no `[P]` markers in those phases.

---

## Parallel Example: Phase 6

```text
# Verify the skill is discoverable, the quickstart passes, and the file writes match contract:
Task T025: Sanity-check skill appears in /-completion list
Task T026: Run full quickstart.md scenario end-to-end (IsoWeek accept + refuse + preview)
Task T027: Contract self-check — diff actual writes against contracts/file-writes.md allow-list
```

T028 (agent-log append) runs after T025–T027 succeed.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) — T001, T002.
2. Complete Phase 2 (Foundational) — T003–T006.
3. Complete Phase 3 (User Story 1) — T007–T017.
4. **STOP and VALIDATE**: With the skill able to scaffold but not refuse cleanly or preview, the MVP is testable. A developer using only the MVP must avoid invoking the skill on a duplicate name (the analysis will detect it, but the refusal message format from US2 won't yet exist — the skill will simply not write anything and emit a terse "can't scaffold" message).
5. Optional: ship the MVP as a checkpoint commit.

### Incremental Delivery

1. Setup + Foundational → skill skeleton exists, recognised by Claude Code (T025 partial pass).
2. + User Story 1 → developers can scaffold real report types. MVP.
3. + User Story 2 → refusal codes and clean error messages.
4. + User Story 3 → preview / dry-run.
5. Polish → contract self-check, agent-log entry for the feature itself.

### Solo Developer Strategy

Because every task in Phases 2–5 edits one file, there is no benefit to splitting the work across multiple developers. A single developer working linearly through the task list is the recommended execution strategy. Phase 6 is the only opportunity for parallel work (T025, T026, T027) and it's a quick verification pass.

---

## Notes

- `[P]` tasks = different files OR independent verifications, no dependencies on incomplete tasks.
- `[Story]` label maps each implementation task to a user story for traceability.
- Manual verification (T017, T021, T024, T026) is the testing model for this feature — there is no automated test of the skill itself.
- After each manual verification that produced scaffold output (T017), run `git restore .` so the verification artifacts don't end up in the feature's commit.
- T028 is the only task that intentionally mutates `ai-artifacts/agent_log.txt` as part of this feature's normal completion. Every prior log mutation in T017 / T021 / T026 came from the skill itself during verification and should be allowed to remain (the log is append-only — those entries are part of the project's AI-assisted-development record).
- Avoid: editing `SKILL.md` out of order (sections depend on the prior section's vocabulary); marking phase-3–5 tasks `[P]` (they all touch the same file); skipping manual verifications (the only test surface this feature has).
