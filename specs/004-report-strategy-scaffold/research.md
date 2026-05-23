# Phase 0 Research: `report-strategy-scaffold` Claude Skill

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

## Scope

Resolve the unknowns and design choices needed before Phase 1 can produce the data model, contracts, and quickstart. Each entry below is a **Decision / Rationale / Alternatives considered** triple — the standard Phase 0 record format.

---

## R-001 — What "name already exists" means for refusal

**Decision**: The skill refuses (FR-014(a)) when an **`IReportStrategy` implementation is already registered for the requested `ReportType`**, not merely when the enum value exists. Detection is by searching `Finance.Business/Services/Reports/` for a file matching `*ReportStrategy.cs` whose class declares `Type => ReportType.<RequestedName>`. The `ReportType` enum itself may declare a value with no strategy attached (this is the current state for `IsoWeek` per `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs:5`).

**Rationale**: The constitution (v3.0.0, Principle II) explicitly mentions `IsoWeek` as a documented enum value with no strategy yet. Treating "enum value present" as "already exists" would block the exact first non-trivial use case for the skill — scaffolding the `IsoWeek` strategy. The runtime symptom of `POST /api/reports` with `{ "type": "IsoWeek" }` today is HTTP 400 (factory returns `null`), proving that the enum value alone is not a complete report type.

**Alternatives considered**:

- *Refuse when the enum value exists.* Rejected — would block IsoWeek scaffolding and force the skill to also remove + re-add the enum value, complicating diffs unnecessarily.
- *Refuse only when both enum value and strategy exist.* This is the chosen rule (the two-condition form collapses to "strategy registered" because the enum value is implied by the strategy's `Type =>` arm).
- *Allow re-scaffold and overwrite an existing strategy file.* Rejected — destructive and silent; a developer asking to "add" `Period` again is almost certainly mistaken about which type they meant.

**Implications for contracts**: `contracts/skill-input-output.md` encodes this as refusal code `R1-strategy-exists` with a refusal message that names the existing strategy file path so the developer can decide whether to remove/rename it before re-invoking.

---

## R-002 — Where the new strategy gets registered in DI

**Decision**: The skill edits **`src/backend/FinanceTracker/Finance.Api/Program.cs`** by inserting one new line of the form `builder.Services.AddSingleton<IReportStrategy, <NewType>ReportStrategy>();` immediately after the existing `PeriodReportStrategy` registration (`Program.cs:30`). The skill MUST NOT touch any other line in `Program.cs`.

**Rationale**: `ReportStrategyFactory`'s constructor takes `IEnumerable<IReportStrategy>` (`ReportStrategyFactory.cs:9`), so the factory itself auto-discovers strategies — **but the DI container only resolves the ones explicitly registered**. The factory does not perform assembly scanning. There is no convention-based discovery layer to fall back on. The single registration line in `Program.cs` is therefore the load-bearing edit; without it, the new strategy compiles, has tests, and never executes at runtime.

**Alternatives considered**:

- *Introduce assembly scanning (Scrutor or hand-rolled).* Rejected — adds a new NuGet dependency for a single use case; obscures the registration in a way that makes the strategy harder to find when reading `Program.cs`. The constitution favours visible, boring DI.
- *Move strategy registration into `Finance.Business` (e.g., an `IServiceCollection` extension method).* Rejected for this feature — out of scope; the project's pattern today is to register everything directly in `Program.cs`. A refactor of that pattern would touch more than this skill's contract permits.
- *Register the strategy lazily inside `ReportStrategyFactory` via a static type list.* Rejected — breaks the `IEnumerable<IReportStrategy>` injection contract that all of `Finance.Business`'s strategy tests rely on.

**Implications for contracts**: `contracts/file-writes.md` lists `Finance.Api/Program.cs` as an "edit one line" file, with a strict diff allowlist: the only permitted modification is adding one `AddSingleton<IReportStrategy, *>()` call adjacent to the existing one. Any other modification is a contract violation.

---

## R-003 — Skill packaging: `SKILL.md` frontmatter shape

**Decision**: The skill is packaged as a single file `.claude/skills/report-strategy-scaffold/SKILL.md`. Frontmatter follows the same shape as existing speckit skills (e.g. `.claude/skills/speckit-specify/SKILL.md`):

```yaml
---
name: "report-strategy-scaffold"
description: "Scaffold a new report type into the Finance.Business factory + strategy pipeline (enum, DTO, strategy, DI registration, unit + integration tests)."
argument-hint: "Describe the new report's name, payload fields, payload→date-range rule, and period descriptor format. Use `--preview` to dry-run."
compatibility: "Requires the post-feature-003 layout (Finance.Business/Services/Reports/PeriodReportStrategy.cs as the anchor file)."
metadata:
  author: "finance-tracker-api"
  source: "specs/004-report-strategy-scaffold/spec.md"
user-invocable: true
disable-model-invocation: false
---
```

**Rationale**: Matches the format `/speckit-specify`, `/speckit-plan`, and the rest of the speckit skill family use, so the skill appears in the user-invocable skill list and Claude can discover it via natural-language requests. `user-invocable: true` and `disable-model-invocation: false` are the two flags that satisfy FR-001 and FR-002 together.

**Alternatives considered**:

- *Multi-file skill bundle (`SKILL.md` + helper scripts under `scripts/`).* Rejected — the skill's logic is reasoning about source files, which the LLM does directly via the Read/Edit/Write tool family. There is no per-platform PowerShell/bash work that would justify a script. Keeping it single-file matches all the speckit skills we already have.
- *Hidden / model-only skill (`user-invocable: false`).* Rejected — FR-002 explicitly requires the skill to be invocable as `/report-strategy-scaffold`. Hiding it would break the documented invocation contract.

---

## R-004 — Resolving payload-to-date-range for non-trivial cases (TODO markers)

**Decision**: When the developer's description includes a payload-to-range rule the skill can derive deterministically (e.g., `Period`'s `[start, end]`, `IsoWeek`'s Monday–Sunday from `yyyy-Www`, `Month`'s first-to-last day from `yyyy-MM`), the skill generates the resolution inline and writes a real unit test asserting the rule.

When the rule is ambiguous, novel, or culture-dependent (e.g., "previous N business days", "current fiscal quarter"), the skill **scaffolds the strategy with a `TODO` marker** in the resolve-to-range method body and **marks the corresponding unit-test method with xUnit's `[Fact(Skip = "…")]` attribute** with a reason naming the missing logic. The final summary message (FR-013) calls out every skipped test by name so the developer knows what to finish.

**Rationale**: A skill that fabricates resolution logic for "previous N business days" without asking what counts as a business day will produce subtly broken aggregations. A skill that refuses any non-trivial rule will be useless for half the report types the project might want. The middle path — scaffold the skeleton, mark the resolution as TODO, mark the test as Skip with a reason — keeps `dotnet build` and `dotnet test` green while making the unfinished work loud and unambiguous (Edge Case 4 in spec.md).

**Alternatives considered**:

- *Always inline the resolution.* Rejected — silent bugs in the date math are exactly the kind of failure mode an AI-assisted scaffold should not introduce. The constitution's "AI workflow is itself a graded deliverable" (Principle V) makes silent fabrication a higher cost than visible TODO markers.
- *Always refuse non-trivial rules.* Rejected — the cost (developer hand-writes the strategy from the Period template) is no better than not having the skill.
- *Mark TODOs but write tests with placeholder asserts.* Rejected — a placeholder assert that "passes" gives a false signal in CI. `[Fact(Skip = …)]` is the honest mechanism.

**Implications for contracts**: `contracts/skill-input-output.md` documents `--preview` and the skip behaviour. The agent-log entry (per `contracts/agent-log-entry.md`) lists skipped tests in the suggestion summary.

---

## R-005 — Refusal protocol (no half-finished writes)

**Decision**: The skill performs **all analysis before any file is written**: read existing source, classify the request, determine whether any refusal condition (FR-014 a–d) holds. Only after analysis decides "accept" does any Write/Edit fire. If the developer cancels mid-scaffold (rare; spec.md Edge Case 7), the skill's final user-facing message lists any partial files for `git restore`.

**Rationale**: The cost of a half-finished scaffold is non-trivial: leftover stub files, a broken build, and a confusing diff to clean up. Doing all analysis up front is also what makes the dry-run mode (FR-017) trivial — it is the accept path minus the writes.

**Alternatives considered**:

- *Write incrementally and validate after each write.* Rejected — exposes the developer to inconsistent intermediate states (e.g., enum value added but strategy file missing). The accept-or-refuse-as-a-batch protocol is what makes FR-015's "`git status --porcelain` MUST be unchanged" testable.
- *Use a temp directory and atomic move.* Rejected — too much machinery for a small set of file writes; the developer's feature branch is the safety net.

---

## R-006 — Dirty-tree handling (FR-016 / Edge Case 2)

**Decision**: At the start of every non-preview invocation, the skill runs `git status --porcelain` (the only `git` command it shells out to). If the output is non-empty, the skill emits a warning, lists the dirty files, and **asks the developer to confirm** ("Proceed anyway?") before continuing. There is no auto-stash / auto-commit step — the speckit `before_implement` hook already handles that opportunistically.

**Rationale**: A multi-file scaffold mixed into unrelated dirty changes produces a diff that's hard to review. Forcing a clean baseline (or an explicit override) keeps the scaffold reviewable as a single self-contained PR. The constitution does not prohibit dirty-tree starts, so the rule is "warn and confirm", not "block".

**Alternatives considered**:

- *Block hard on any dirty tree.* Rejected — too strict; the developer may legitimately have unrelated WIP they want to keep.
- *Auto-commit unrelated changes first.* Rejected — would push speckit's hook responsibilities into this skill; mixing concerns.

**Note**: This is the **only** `git` shell-out in the skill. The skill never `git add`, `git commit`, `git restore`, or `git push`.

---

## R-007 — How the skill validates `dotnet build` / `dotnet test` results without dumping huge logs

**Decision**: The skill captures the exit code and the last ~60 lines of stdout/stderr from each invocation. On non-zero exit, it greps the output for the first occurrence of one of:

- For `dotnet build`: lines matching `error CS\d+:` (compile errors) or `error MSB\d+:` (MSBuild errors).
- For `dotnet test`: lines matching `Failed `, `[xUnit.net …]`, or `Test Run Failed`.

It then prints **only those matched lines plus 3 lines of trailing context** to the developer, and writes the same trimmed excerpt into the agent-log entry. The full log is not retained.

**Rationale**: FR-011 requires the skill to "trim the output to the first failing error or test name rather than dumping the full log". The two regex families above cover the >95% of practical failure modes in this project's toolchain (csc + xunit.v3). The "first match + 3 lines of context" rule keeps the developer's terminal scannable.

**Alternatives considered**:

- *Print full logs.* Rejected by FR-011.
- *Print only exit codes.* Rejected — the developer would have to re-run the commands by hand to learn what failed.
- *Persist logs to `.cache/` for later inspection.* Rejected — adds new files outside the contract.

---

## R-008 — Style of generated code (records, namespaces, comments)

**Decision**: The skill emits code in the same style observed in the existing `Finance.Business/Services/Reports/PeriodReportStrategy.cs` and `Finance.Business/Dtos/Reports/PeriodReportData.cs`:

- File-scoped namespaces.
- DTOs as `public sealed record`s with positional parameters.
- Strategies as `public sealed class` types with primary-constructor DI of `ITransactionRepository` and `ICategoryRepository`.
- No XML doc comments.
- No inline comments unless the *why* is non-obvious (per CLAUDE.md's "default to writing no comments" rule).
- Validation errors use the existing `Finance.Business/Validation/ValidationError` shape, thrown via `ReportValidationException`.

**Rationale**: Matching the surrounding style is necessary for SC-004 (the generated strategy looks the same as `PeriodReportStrategy` side-by-side). Diverging style — XML comments, classic constructors, `internal` modifiers — would be reviewer-visible noise without adding value.

**Alternatives considered**: None worth recording; this is a "match what's there" decision.

---

## R-009 — Test scaffolding fidelity to `PeriodReportStrategyTests`

**Decision**: The generated `<NewType>ReportStrategyTests.cs` mirrors the structure of the existing `PeriodReportStrategyTests.cs`:

- One test class per strategy.
- Moq strict mode for `ITransactionRepository` and `ICategoryRepository` mocks (matching the project's existing usage per CLAUDE.md "Tests" section).
- xUnit v3 `[Fact]`s with `Arrange / Act / Assert` blocks separated by blank lines.
- One test per FR-008 bullet (happy path, invalid payload → `ReportValidationException`, payload-shape edge cases, `period` descriptor format).

The generated integration test in `ReportsEndpointTests.cs` follows the existing pattern in that file: one `[Fact]` per scenario, reusing the `ApiTestFixture` to drive `POST /api/reports`.

**Rationale**: Predictable test shape is what makes SC-002 (3 different report types all build+pass on first invocation) achievable. A reviewer skimming the new test file should recognise it instantly as "another `PeriodReportStrategyTests`-shaped file".

**Alternatives considered**: None — fidelity to the template is the entire point.

---

## R-010 — Updating `ReportStrategyFactoryTests` (test that the factory resolves the new type)

**Decision**: The skill **adds a single new test** to the existing `Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs`, asserting `factory.TryGet(ReportType.<NewType>)` returns a non-null `IReportStrategy` whose `Type` equals the new enum value. It does **not** rewrite or restructure the existing tests in that file.

**Rationale**: Principle IV's required coverage area "Report strategy selection by `ReportType` enum value (unknown value → 400)" is satisfied today by the existing tests in this file. The new test is the smallest possible extension that proves the new type is wired into the factory.

**Alternatives considered**:

- *Skip the factory test, rely on integration test only.* Rejected — a unit test on the factory is cheaper, faster, and points more precisely at "DI wiring broken" than an integration test would.
- *Parameterize the existing factory tests with `[Theory]` over all `ReportType`s.* Rejected — would force the skill to edit existing tests in a way that risks breaking unrelated assertions; the standalone `[Fact]` is the lower-risk edit.

---

## Open questions

None. All design questions raised by the spec are resolved above.

## Summary of decisions feeding Phase 1

| Decision | Feeds |
|---|---|
| R-001 (refusal trigger) | `contracts/skill-input-output.md` refusal-code table; `data-model.md` |
| R-002 (DI registration in `Program.cs`) | `contracts/file-writes.md` allow-list; `quickstart.md` worked example |
| R-003 (frontmatter shape) | `data-model.md` skill-manifest entity |
| R-004 (TODO + Skip for non-trivial rules) | `contracts/skill-input-output.md`; `contracts/agent-log-entry.md` |
| R-005 (refuse-as-batch) | `contracts/file-writes.md` no-op guarantee |
| R-006 (dirty-tree confirm) | `contracts/skill-input-output.md` |
| R-007 (log trimming rules) | `contracts/agent-log-entry.md` |
| R-008 (code style) | `data-model.md` generated-artifacts entity |
| R-009 (test fidelity) | `data-model.md` generated-artifacts entity; `quickstart.md` |
| R-010 (factory test extension) | `contracts/file-writes.md` edit list |
