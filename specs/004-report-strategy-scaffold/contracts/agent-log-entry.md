# Contract: Agent Log Entry

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

Defines the shape of the entry the `report-strategy-scaffold` skill appends to `ai-artifacts/agent_log.txt` on every real-run invocation (accept or refuse). Dry-run invocations append nothing (FR-017).

## Block format

Each entry is a plain-text block separated from the previous entry by exactly one blank line. The skill MUST append, never overwrite.

```
[<timestamp>] <tool>
prompt: <verbatim developer input>
suggestion: <one-line summary>
decision: <accepted | rejected | needs-revision>
reason: <free-text>
files: <comma-separated list of paths, or "0">
build: <unknown | success | failure[: <trimmed excerpt>]>
tests: <unknown | success | failure[: <trimmed excerpt>]>
```

Field order is fixed. Multi-line values are not permitted (long `reason` or `suggestion` text is truncated to one line; long `prompt` values are preserved as one line by collapsing internal newlines to spaces — quoting is the existing convention in `ai-artifacts/agent_log.txt`).

## Field semantics

### `timestamp`

ISO-8601 local datetime with offset: `yyyy-MM-ddTHH:mm:sszzz` (e.g., `2026-05-23T14:32:11+02:00`). Resolution is to the second; sub-second precision is not used.

### `tool`

The verbatim string `Claude Code (skill: report-strategy-scaffold)`. If the skill is invoked through Claude Code's natural-language router rather than `/report-strategy-scaffold`, the string is still `Claude Code (skill: report-strategy-scaffold)` — the model name is not included (it is implicit from the surrounding session).

### `prompt`

The developer's verbatim input following `/report-strategy-scaffold`. For natural-language invocations, the user's triggering message. Multi-line prompts are collapsed to one line by replacing newlines with `" \\n "` (literal backslash-n, surrounded by spaces) so the log remains line-oriented.

### `suggestion`

One line summarising what the skill proposed:

- **Accept path**: `Scaffold <Name>ReportStrategy: add enum, DTO, strategy, DI line, 2 tests`.
- **Refuse path**: `Refuse: <refusal-code> — <one-clause rationale>` (e.g., `Refuse: R1-strategy-exists — IsoWeekReportStrategy already at Finance.Business/Services/Reports/IsoWeekReportStrategy.cs`).
- **Needs-revision path** (build or tests failed after writes): `Scaffold <Name>ReportStrategy: writes applied, build/test failed — see reason`.

### `decision`

One of three exact strings:

- `accepted` — writes applied successfully **and** `dotnet build` returned 0 **and** `dotnet test` returned 0 (or returned 0 with only skipped tests, which is the non-trivial-rule case from R-004).
- `rejected` — no writes applied; a refusal code from `contracts/skill-input-output.md` was emitted.
- `needs-revision` — writes applied but `dotnet build` or `dotnet test` returned non-zero. This is the honest "I tried, here's what broke" state; the developer is responsible for follow-up.

### `reason`

Free-text justification:

- `accepted` — short, e.g., `Build green, tests green` or `Build green, 1 test skipped (resolve-to-range TODO)`.
- `rejected` — names the FR-014 sub-rule or constitution principle violated. Example: `FR-014(a) — IReportStrategy for ReportType.Period is already registered (PeriodReportStrategy.cs:10).`
- `needs-revision` — carries the trimmed excerpt from `dotnet build`/`dotnet test` per research.md R-007. Example: `dotnet build failed: error CS0246: The type or namespace name 'IReportStrategy' could not be found …`.

### `files`

Comma-separated list of paths the skill modified, **relative to the repo root**. On the refuse path this is the literal string `0` (no paths). The append to `ai-artifacts/agent_log.txt` itself is **not** listed here (it would be self-referential).

Accept-path example (for an `IsoWeek` scaffold):

```
files: src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs, src/backend/FinanceTracker/Finance.Business/Dtos/Reports/IsoWeekReportData.cs, src/backend/FinanceTracker/Finance.Business/Services/Reports/IsoWeekReportStrategy.cs, src/backend/FinanceTracker/Finance.Api/Program.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/IsoWeekReportStrategyTests.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs, src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs
```

### `build`

One of:

- `unknown` — the skill did not run `dotnet build` (refuse path, dry-run path).
- `success` — `dotnet build` returned 0 with no errors.
- `failure: <first matched error per R-007>` — `dotnet build` returned non-zero; the first compile/MSBuild error matched by R-007's regex family is included.

### `tests`

One of:

- `unknown` — the skill did not run `dotnet test` (refuse path, dry-run path, or `build` did not succeed).
- `success` — `dotnet test` returned 0; all tests passed. If `[Fact(Skip = …)]` tests were emitted, they are reported as `success (N skipped: <names>)`.
- `failure: <first matched failure per R-007>` — `dotnet test` returned non-zero; the first failing test name + its first stack line is included.

## Worked examples

### Example 1 — Accepted (IsoWeek, trivial rule)

```
[2026-05-23T14:32:11+02:00] Claude Code (skill: report-strategy-scaffold)
prompt: Add an IsoWeek report. Payload is { week: "yyyy-Www" }. Resolves to Monday-Sunday of that ISO week. Period descriptor: yyyy-Www.
suggestion: Scaffold IsoWeekReportStrategy: add enum (already present), DTO, strategy, DI line, 2 tests
decision: accepted
reason: Build green, tests green
files: src/backend/FinanceTracker/Finance.Business/Dtos/Reports/IsoWeekReportData.cs, src/backend/FinanceTracker/Finance.Business/Services/Reports/IsoWeekReportStrategy.cs, src/backend/FinanceTracker/Finance.Api/Program.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/IsoWeekReportStrategyTests.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs, src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs
build: success
tests: success
```

### Example 2 — Rejected (Period already exists)

```
[2026-05-23T15:01:44+02:00] Claude Code (skill: report-strategy-scaffold)
prompt: Add a Period report.
suggestion: Refuse: R1-strategy-exists — PeriodReportStrategy already at Finance.Business/Services/Reports/PeriodReportStrategy.cs
decision: rejected
reason: FR-014(a) — IReportStrategy for ReportType.Period is already registered (PeriodReportStrategy.cs:10).
files: 0
build: unknown
tests: unknown
```

### Example 3 — Needs revision (tests failed)

```
[2026-05-23T16:14:02+02:00] Claude Code (skill: report-strategy-scaffold)
prompt: Add a Month report. Payload { month: "yyyy-MM" }. Resolves to first..last day of that month. Descriptor yyyy-MM.
suggestion: Scaffold MonthReportStrategy: writes applied, build/test failed — see reason
decision: needs-revision
reason: dotnet test failed: MonthReportStrategyTests.Happy_path_aggregates_february_correctly — Assert.Equal() Failure: Expected 28, got 29
files: src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs, src/backend/FinanceTracker/Finance.Business/Dtos/Reports/MonthReportData.cs, src/backend/FinanceTracker/Finance.Business/Services/Reports/MonthReportStrategy.cs, src/backend/FinanceTracker/Finance.Api/Program.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/MonthReportStrategyTests.cs, src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs, src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs
build: success
tests: failure: MonthReportStrategyTests.Happy_path_aggregates_february_correctly — Assert.Equal() Failure: Expected 28, got 29
```

## Invariants

- Every real-run invocation produces exactly one entry. No more, no fewer.
- The `decision` field never lies: `accepted` implies green build and tests; `rejected` implies no writes; `needs-revision` implies writes applied but verification failed.
- The `files` field is the ground truth for what to review: the developer can `git diff -- <files>` and see the exact scaffold.
