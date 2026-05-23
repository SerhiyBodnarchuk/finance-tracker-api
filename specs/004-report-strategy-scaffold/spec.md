# Feature Specification: `report-strategy-scaffold` Claude Skill

**Feature Branch**: `004-report-strategy-scaffold`

**Created**: 2026-05-23

**Status**: Draft

**Input**: User description: "Create custom claude skill to generate new types of report. Skill should modify the codebase including tests. For examples we can use period report."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scaffold a new report type end-to-end (Priority: P1)

A developer working in this repository wants to add a new report type (for example, `IsoWeek`) without writing the boilerplate by hand. They invoke the `report-strategy-scaffold` skill in Claude Code, describing the new report's name, its request payload shape, and the rule for turning that payload into a `[start, end]` date range. The skill then edits the codebase across all three layers, adds tests that mirror the structure of the existing `PeriodReport*` tests, runs `dotnet build` and `dotnet test`, and reports back what it changed. After the run, `POST /api/reports` with the new `type` value works end-to-end against the seeded in-memory data, and the existing report behaviour is unchanged.

**Why this priority**: This is the entire reason the skill exists. The constitution and `CLAUDE.md` already promise that the report-strategy pattern is "the template for the project's reusable AI skill" — until that skill produces a working new report end-to-end, the promise is unfulfilled. Every later capability (rejection of bad inputs, agent-log discipline, dry-run mode) is meaningful only on top of this one. Build this slice first and the developer can already scaffold a real second report type (IsoWeek or Month) with one invocation.

**Independent Test**: From a clean checkout on the `004-report-strategy-scaffold` branch, a developer invokes the skill with an input describing an `IsoWeek` report (`data.week` field of the form `"yyyy-Www"`, resolves to Monday–Sunday). After the skill completes, all of the following are true without further manual edits: (a) `dotnet build` succeeds; (b) `dotnet test` passes, including new strategy unit tests under `Finance.Business.UnitTests/Services/Reports/` and at least one new integration test asserting `POST /api/reports` with `type: "IsoWeek"` returns HTTP 200 against the seed data; (c) the existing `Period` report tests still pass unchanged; (d) `ai-artifacts/agent_log.txt` has a new entry describing the invocation, the prompt, the files touched, and the build/test outcome.

**Acceptance Scenarios**:

1. **Given** the working tree is clean on the feature branch, **When** a developer invokes the skill with a fully-specified new report type (name, payload fields, payload → date-range rule, validation rules), **Then** the skill (a) adds a new value to the `ReportType` enum in `Finance.Business`, (b) creates a new `*ReportData` record under `Finance.Business/Dtos/Reports/`, (c) creates a new `*ReportStrategy` class under `Finance.Business/Services/Reports/` that implements `IReportStrategy`, (d) registers the new strategy with `ReportStrategyFactory`, (e) creates a unit-test file under `Finance.Business.UnitTests/Services/Reports/` mirroring the `PeriodReportStrategyTests` layout, (f) updates or adds an integration test under `Finance.Api.IntegrationTests/` covering the new `type` value, and (g) leaves the existing `Period` report code and tests untouched.
2. **Given** the skill has finished scaffolding, **When** the developer (or the skill itself) runs `dotnet build` and `dotnet test` from `src/backend/FinanceTracker`, **Then** both commands exit with code `0` and no warnings introduced by the generated code.
3. **Given** the skill has finished scaffolding, **When** the developer starts the API with `dotnet run --project Finance.Api` and sends `POST /api/reports` with `{ "type": "<NewType>", "data": { ... } }` matching the new payload, **Then** the response is HTTP 200 with a `ReportResult` body whose `type` field matches the new type's name and whose `period` descriptor matches the format the skill input specified.
4. **Given** the skill has finished scaffolding, **When** the developer opens `ai-artifacts/agent_log.txt`, **Then** there is a new entry timestamped with the current run, identifying the model/tool, the user's prompt, the suggestion summary, the decision (accepted), and the list of files changed.
5. **Given** the new report type is in place, **When** the developer sends `POST /api/reports` with the previously-working `{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }`, **Then** the response is byte-identical to the response that would have been returned before the skill ran (existing behaviour is preserved).

---

### User Story 2 - Refuse to scaffold when the request conflicts or is unsupported (Priority: P2)

A developer asks the skill to add a report type whose name collides with an existing `ReportType` enum value, or whose shape doesn't fit the parse-typed-data → resolve-to-range → filter → aggregate → sort-breakdown pattern (for example: a streaming report, a multi-period diff report, or one that needs persistence). The skill detects the mismatch before touching the codebase and refuses, explaining what it would need to change and why that change is out of bounds for the constitution. No files are modified.

**Why this priority**: The skill writes to disk and to the agent log; a wrong scaffold leaves a half-finished implementation that the developer then has to either fix by hand or `git reset`. Detecting "I can't safely do this" before any write is what separates a useful scaffold from a footgun. It also protects the constitution invariants (three-layer dependency direction, no persistence, strategy-pattern shape) from being silently violated by a generated stub.

**Independent Test**: Invoke the skill twice from a clean working tree. First, ask for a report type named `Period` (already exists). Second, ask for a "monthly diff that compares two months and persists the result." In both cases, after the skill exits: (a) `git status --porcelain` is empty (no files changed), (b) `git log -1 --oneline` is the same commit as before the invocation, (c) the skill's output to the developer names the specific rule it would have violated (duplicate enum value; persistence required; multi-range payload), and (d) no entry is appended to `ai-artifacts/agent_log.txt` claiming the change was accepted (a rejection entry MAY be appended, but it MUST be marked as `decision: rejected`).

**Acceptance Scenarios**:

1. **Given** the `ReportType` enum already contains a value `Period`, **When** the developer invokes the skill with a new report named `Period`, **Then** the skill detects the duplicate, exits without modifying any file in the solution, and tells the developer that the name is already taken (case-insensitive comparison).
2. **Given** the developer describes a report whose payload-to-range step cannot be expressed as a single `[start, end]` window (e.g., a diff between two windows), **When** the skill analyses the input, **Then** it refuses, explaining that the strategy contract requires one inclusive date range per request and pointing the developer at the constitution rule.
3. **Given** the developer describes a report that must persist its result (e.g., "cache the last response per user"), **When** the skill analyses the input, **Then** it refuses, citing the constitution rule that reports are ad-hoc and never persisted, and lists no files as changed.
4. **Given** any rejection path above, **When** the developer inspects the working tree afterwards, **Then** there are zero file modifications, zero new files, and the agent-log entry (if any) records the rejection rather than a fabricated success.

---

### User Story 3 - Preview the changes before writing (dry-run) (Priority: P3)

A developer is unsure whether the skill will name files the way they want, or whether it will pick the right test layout, so they invoke it in a "preview" mode. The skill walks through the same analysis as a real run, prints the list of files it would create or modify with a short diff summary for each, but writes nothing to disk. The developer reads the preview and either re-invokes the skill in normal mode (if it looks right) or refines their description first.

**Why this priority**: Useful but not essential. A developer who's done it once already trusts the skill enough to skip preview; a developer who wants safety can also just run the skill on a feature branch and `git restore` if they don't like the output. This is a nice-to-have for first-time use and for screencast/demo scenarios, but it should not block P1 or P2.

**Independent Test**: With a clean working tree, invoke the skill in preview mode with the same input that produced a successful real run in story 1. After the preview exits: (a) `git status --porcelain` is empty, (b) the skill's output lists every file it would have created or modified, with the same paths a real run would have used, and (c) `ai-artifacts/agent_log.txt` is unchanged.

**Acceptance Scenarios**:

1. **Given** a fully-specified report description, **When** the developer invokes the skill with a preview/dry-run flag (e.g., `--preview`), **Then** the skill prints a plan listing each file path it would create or modify, plus a one-paragraph summary of the strategy it would generate, and exits without writing.
2. **Given** the preview output, **When** the developer re-invokes the skill without the preview flag and with the same input, **Then** the files actually created/modified exactly match the paths that the preview listed.

---

### Edge Cases

- The developer's input names the new report type with characters that aren't valid as a C# identifier (spaces, hyphens, leading digits) — the skill normalises to PascalCase for the enum value and the type name, reports the normalised name back to the developer, and refuses only if no reasonable normalisation exists.
- The working tree is dirty before the skill runs — the skill warns and asks the developer to commit or stash before modifying multiple files, so its changes can be reviewed as a single diff (the `before_implement` git hook is the longer-term place this lives, but this skill MUST also notice).
- `dotnet build` or `dotnet test` fails after scaffolding — the skill reports the failure honestly (build output trimmed to the first failing error, test failure names) and does **not** claim success in the agent-log entry; the entry is written with `decision: needs-revision` so the log isn't silently misleading.
- The developer's input describes a payload whose date-range derivation is non-trivial (e.g., "the previous N business days") — the skill scaffolds the strategy with a clearly-marked `TODO` for the resolution step and marks the corresponding unit test with xUnit's `Skip` reason so `dotnet test` still passes; the skill reports the skipped test explicitly so it isn't a silent stub.
- The skill is invoked from a directory that is not this repo's root or from a checkout that doesn't have `Finance.Business/Services/Reports/PeriodReportStrategy.cs` — the skill detects the missing anchor file and refuses with a message pointing the developer at the expected working directory.
- The new report type's payload shape duplicates an existing one (e.g., `start` + `end` again) — the skill notices the structural collision and asks the developer to confirm before scaffolding, since this is usually a mistake (the existing `Period` payload would have sufficed) but is occasionally intentional.
- The developer cancels mid-scaffold (Ctrl+C, IDE close) — partial files left on disk are listed in the skill's final message so the developer knows what to `git restore`; this is acceptable because the feature branch is the safety net.

## Requirements *(mandatory)*

### Functional Requirements

#### Skill packaging and invocation

- **FR-001**: The repository MUST contain a Claude Code skill named `report-strategy-scaffold`, packaged under `.claude/skills/report-strategy-scaffold/` with at least a `SKILL.md` file whose frontmatter declares `name`, `description`, an `argument-hint`, and `user-invocable: true` so it appears in the user-invocable skill list.
- **FR-002**: The skill MUST be invocable as `/report-strategy-scaffold` and MUST also be discoverable to Claude when the user asks in natural language to "add a new report type" or equivalent, without the user having to remember the skill's exact name.
- **FR-003**: The skill MUST accept its input as the free-text arguments following the slash command; if the input is empty, the skill MUST prompt for a report type name, the request payload shape, the rule for resolving the payload to a `[start, end]` date range, and the `period` descriptor format, and MUST NOT modify any file until that information is supplied.

#### Codebase modifications produced by the skill

- **FR-004**: On a successful scaffold, the skill MUST add a new value to the `ReportType` enum in `Finance.Business`, using the user-supplied name normalised to PascalCase, and MUST place the new value alphabetically (or at the end — the skill MUST pick one rule and apply it consistently across runs).
- **FR-005**: On a successful scaffold, the skill MUST create exactly one new record file under `Finance.Business/Dtos/Reports/` named `<NewType>ReportData.cs`, containing the fields the developer described, all typed appropriately (dates as `DateOnly`, strings as `string`, integers as `int`).
- **FR-006**: On a successful scaffold, the skill MUST create exactly one new class file under `Finance.Business/Services/Reports/` named `<NewType>ReportStrategy.cs`, implementing `IReportStrategy`, following the parse-typed-data → resolve-to-range → filter → aggregate → sort-breakdown shape of `PeriodReportStrategy.cs`.
- **FR-007**: On a successful scaffold, the skill MUST register the new strategy with `ReportStrategyFactory` so that `POST /api/reports` with `{ "type": "<NewType>", "data": { ... } }` resolves to the new strategy at runtime; the registration MUST not require any DI-container edit outside of `Finance.Business` if the factory is built that way today, and MUST not touch the `Finance.Api` composition root if the existing factory already auto-discovers strategies.
- **FR-008**: On a successful scaffold, the skill MUST create a unit-test file under `Finance.Business.UnitTests/Services/Reports/<NewType>ReportStrategyTests.cs` covering at least: (a) the happy-path aggregation against fixed input transactions, (b) invalid payload → `ReportValidationException`, (c) out-of-range or malformed inputs specific to the new payload shape, and (d) the `period` descriptor formatting. Tests MUST use xUnit v3's built-in `Xunit.Assert` API and Moq 4.20.x for any collaborator (per CLAUDE.md), never FluentAssertions.
- **FR-009**: On a successful scaffold, the skill MUST add at least one integration test in `Finance.Api.IntegrationTests` that posts to `/api/reports` with the new `type` value against the seeded in-memory data and asserts an HTTP 200 with a `ReportResult` body whose `type` field matches.
- **FR-010**: On a successful scaffold, the skill MUST NOT modify any file in `Finance.Data` (the data layer is intentionally not involved in the report-strategy pattern), MUST NOT modify any file in `Finance.Api` other than tests, and MUST NOT modify the `Period` report's existing files.

#### Verification and reporting

- **FR-011**: After applying the scaffold, the skill MUST run `dotnet build` from `src/backend/FinanceTracker` and MUST run `dotnet test` from the same directory, and MUST report both results to the developer; if either fails, the skill MUST trim the output to the first failing error or test name rather than dumping the full log.
- **FR-012**: The skill MUST append an entry to `ai-artifacts/agent_log.txt` recording the timestamp, the model/tool name, the verbatim user prompt, a one-line suggestion summary, the developer's decision (accepted/rejected/needs-revision), and the list of files created or modified. Rejection paths from FR-014 MUST also produce a log entry, but marked `decision: rejected`.
- **FR-013**: The skill MUST report back to the developer a final message that includes: the new `ReportType` enum value, the paths of all files created or modified, the `dotnet build` and `dotnet test` outcomes, and either "ready to commit" or an explicit list of follow-ups the developer must complete (e.g., skipped tests, TODO markers in the resolution step).

#### Refusal and safety

- **FR-014**: The skill MUST refuse to scaffold and MUST NOT modify any file when any of the following hold: (a) the requested report-type name (case-insensitive) already exists in the `ReportType` enum; (b) the requested behaviour requires persistence, caching, or any storage beyond in-memory aggregation; (c) the requested payload cannot be reduced to a single inclusive `[start, end]` date range per request; (d) the anchor file `Finance.Business/Services/Reports/PeriodReportStrategy.cs` is not present (indicating the skill is being run in the wrong directory or in a checkout that predates feature 003).
- **FR-015**: When the skill refuses, it MUST tell the developer which specific rule from FR-014 (or which constitution principle) it would have violated, and MUST list zero file changes; on disk, `git status --porcelain` MUST be unchanged from before the invocation.
- **FR-016**: When the working tree is dirty before the skill runs, the skill MUST warn the developer and ask for explicit confirmation before applying multi-file changes, so that the scaffold appears as a single reviewable diff.

#### Preview / dry-run

- **FR-017**: The skill MUST support a preview mode (invoked via a flag in the skill input — exact spelling determined at implementation time, e.g. `--preview` or `--dry-run`) that performs the same analysis as a real run but writes nothing to disk and appends nothing to `ai-artifacts/agent_log.txt`; the preview output MUST list the exact file paths a real run would produce.

### Key Entities

- **Skill manifest (`SKILL.md`)**: The markdown file under `.claude/skills/report-strategy-scaffold/` whose frontmatter (`name`, `description`, `argument-hint`, `user-invocable`, `disable-model-invocation`) makes the skill discoverable to Claude Code and whose body is the prompt Claude executes when the skill is invoked.
- **Report type input**: The free-text description supplied by the developer. Its meaningful parts are: the new report's name, the field list of the request payload, the rule for turning that payload into a `[start, end]` range, the format of the `period` descriptor string, and any payload-specific validation rules.
- **Generated artifacts**: The collection of files the skill creates or modifies for a single successful scaffold — one enum-value edit, one new `*ReportData.cs`, one new `*ReportStrategy.cs`, one factory registration edit, one new unit-test file, one new (or extended) integration-test file, and one new entry in `ai-artifacts/agent_log.txt`.
- **Refusal record**: When the skill refuses, the structured fact it reports back — the rule violated, the developer-facing explanation, and the (empty) list of file changes. May be appended to `ai-artifacts/agent_log.txt` with `decision: rejected`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who has not previously used the skill can add a working new report type — enum value, payload DTO, strategy, factory registration, unit tests, integration test, agent-log entry, and a successful build+test — in **under 5 minutes of wall-clock time**, end-to-end, from invoking the skill to seeing `dotnet test` pass.
- **SC-002**: For at least **3 different report-type descriptions** representative of the kinds the project might add next (e.g., ISO week, calendar month, last-N-days), the skill produces code that builds and tests that pass on the first invocation, without manual edits between scaffold and `dotnet test`.
- **SC-003**: When the skill is invoked with an input that violates any rule in FR-014, **100%** of those invocations leave `git status --porcelain` empty, with no new files, no modified files, and no successful-decision entry in `ai-artifacts/agent_log.txt`.
- **SC-004**: The generated strategy class has the same five-step shape as `PeriodReportStrategy` (parse typed data → resolve to range → filter transactions → aggregate totals and breakdown → sort breakdown), verifiable by a reviewer reading the two files side-by-side — measured by spot-checks on each scaffolded strategy.
- **SC-005**: Every invocation of the skill — accepted **or** rejected — results in exactly one new entry in `ai-artifacts/agent_log.txt` whose `decision` field accurately reflects what happened on disk (no silent successes, no silent rejections).
- **SC-006**: Existing `Period` report behaviour — the response body of `POST /api/reports` with the canonical period payload — is **byte-identical** before and after a scaffold run for any other report type.

## Assumptions

- The skill is invoked from inside this repository's working tree, with `Finance.Business/Services/Reports/PeriodReportStrategy.cs` present (i.e., feature 003 is already merged). The skill detects and refuses other cases per FR-014(d).
- The skill is run by a developer working on a feature branch, where multi-file diffs can be safely reviewed and reverted; it is not designed to run against `main` directly.
- `dotnet`, the .NET 10 SDK, and the project's NuGet feeds are available on the developer's machine — the skill does not attempt to install them.
- The skill produces code in the project's existing style: file-scoped namespaces, records for DTOs and entities, primary constructors where applicable, no XML doc comments, and no inline comments unless the *why* is non-obvious (per CLAUDE.md's "default to writing no comments" rule).
- The skill targets `net10.0` (per CLAUDE.md) and does not generate code that would compile only against earlier TFMs.
- The skill does not generate documentation files (README, markdown notes) unless the developer asks for them explicitly — consistent with the project's "never create documentation files unless explicitly requested" rule.
- The integration-test additions reuse the existing `WebApplicationFactory<Program>` infrastructure from `Finance.Api.IntegrationTests`; the skill does not introduce a new test harness or runner.
- The skill is not responsible for renaming or evolving existing report types; it only adds new ones. Removal or rename of an existing report type is a manual operation, out of scope for this skill.
- The skill is local-only: it does not make network calls, does not call out to external APIs, and does not require credentials. Any LLM reasoning happens inside the Claude Code session that invoked it.
