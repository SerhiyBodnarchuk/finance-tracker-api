# Quickstart: `report-strategy-scaffold` Claude Skill

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

A worked end-to-end run of the skill, using `IsoWeek` as the new report type. This is the canonical demo for SC-001 ("under 5 minutes wall-clock") and is what `quickstart.md` exists to provide.

## Pre-conditions

1. Working tree on branch `004-report-strategy-scaffold` (the implementing branch), or any descendant.
2. Solution at `src/backend/FinanceTracker/FinanceTracker.slnx` builds and tests pass on the current `HEAD`.
3. `.claude/skills/report-strategy-scaffold/SKILL.md` exists (this feature's deliverable).
4. `Finance.Business/Services/Reports/PeriodReportStrategy.cs` is present (the anchor file from FR-014(d) / R-001).

## Invocation

In a Claude Code session at the repo root:

```
/report-strategy-scaffold Add an IsoWeek report. Payload is { week: "yyyy-Www" }. Resolves to Monday–Sunday of that ISO week. Period descriptor format: yyyy-Www.
```

## Expected interaction transcript

```
skill: Plan:
       1. Skip ReportType enum add — IsoWeek already declared at Finance.Business/Enums/ReportType.cs:6
       2. Create Finance.Business/Dtos/Reports/IsoWeekReportData.cs
          (public sealed record IsoWeekReportData(string Week);)
       3. Create Finance.Business/Services/Reports/IsoWeekReportStrategy.cs
          (mirrors PeriodReportStrategy; parse → validate "yyyy-Www" → resolve to
           Monday–Sunday → filter → aggregate → sort)
       4. Edit Finance.Api/Program.cs — add one DI line after PeriodReportStrategy registration
       5. Create tests/Finance.Business.UnitTests/Services/Reports/IsoWeekReportStrategyTests.cs
       6. Edit tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs — one new [Fact]
       7. Edit tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs — one new [Fact]
       Apply? (y/n)

dev:   y

skill: Writes applied (6 files; enum skipped).
       Running dotnet build … success.
       Running dotnet test  … success (47 passed, 0 skipped).
       Appended ai-artifacts/agent_log.txt entry [decision: accepted].

       Summary:
       - New report type: IsoWeek
       - Changed files:
           src/backend/FinanceTracker/Finance.Business/Dtos/Reports/IsoWeekReportData.cs (new)
           src/backend/FinanceTracker/Finance.Business/Services/Reports/IsoWeekReportStrategy.cs (new)
           src/backend/FinanceTracker/Finance.Api/Program.cs (1 line)
           src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/IsoWeekReportStrategyTests.cs (new)
           src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs (1 [Fact])
           src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs (1 [Fact])
       - Build: green
       - Tests: green (47 pass)
       - Ready to commit.
```

## What the developer verifies after the skill exits

### 1. The strategy compiles and is wired up

Open `src/backend/FinanceTracker/Finance.Business/Services/Reports/IsoWeekReportStrategy.cs`. Confirm:

- `public sealed class IsoWeekReportStrategy(ITransactionRepository transactions, ICategoryRepository categories) : IReportStrategy`
- `ReportType Type => ReportType.IsoWeek;`
- `Generate(ReportRequest)` parses `request.Data` to `IsoWeekReportData`, validates `Week` is `"yyyy-Www"`, resolves to a Monday–Sunday range, filters / aggregates / sorts — same shape as `PeriodReportStrategy`.

Open `src/backend/FinanceTracker/Finance.Api/Program.cs`. Confirm a new line `builder.Services.AddSingleton<IReportStrategy, IsoWeekReportStrategy>();` appears immediately after the existing `PeriodReportStrategy` registration (the existing line at `Program.cs:30` is unchanged).

### 2. The new tests pass

```powershell
cd src/backend/FinanceTracker
dotnet build
dotnet test
```

Both commands exit 0. `IsoWeekReportStrategyTests` has at least four `[Fact]`s (happy path, invalid payload → `ReportValidationException`, malformed `Week` string, period-descriptor formatting). `ReportStrategyFactoryTests` has one new `[Fact]` resolving `ReportType.IsoWeek`. `ReportsEndpointTests` has one new `[Fact]` posting `{ "type": "IsoWeek", "data": { "week": "2026-W19" } }` and asserting HTTP 200 + `type: "IsoWeek"` in the body.

### 3. The `Period` behaviour is unchanged

```powershell
dotnet run --project Finance.Api
```

In a second shell:

```powershell
curl -k -X POST https://localhost:7266/api/reports `
    -H "Content-Type: application/json" `
    -d '{ "type": "Period", "data": { "start": "2026-05-01", "end": "2026-05-31" } }'
```

The response body is byte-identical to what the same call returned before the skill ran (SC-006).

### 4. The agent log

```powershell
Get-Content ai-artifacts/agent_log.txt -Tail 9
```

Shows exactly the block defined in `contracts/agent-log-entry.md` §Worked examples Example 1.

## Refusal demo (Story 2)

Re-invoke immediately:

```
/report-strategy-scaffold Add an IsoWeek report. Payload { week: "yyyy-Www" }. …
```

Expected:

```
skill: Refuse: R1-strategy-exists — IsoWeekReportStrategy already at
       Finance.Business/Services/Reports/IsoWeekReportStrategy.cs
       Files changed: 0
       Appended ai-artifacts/agent_log.txt entry [decision: rejected].
```

`git status --porcelain` shows only `ai-artifacts/agent_log.txt` modified (the log append is the only side effect of a refusal).

## Preview demo (Story 3)

```
/report-strategy-scaffold --preview Add a Month report. Payload { month: "yyyy-MM" }. …
```

Expected:

```
skill: Plan (preview, no writes):
       1. Add ReportType.Month to Finance.Business/Enums/ReportType.cs
       2. Create Finance.Business/Dtos/Reports/MonthReportData.cs
       3. Create Finance.Business/Services/Reports/MonthReportStrategy.cs
       4. Edit Finance.Api/Program.cs — 1 DI line
       5. Create tests/Finance.Business.UnitTests/Services/Reports/MonthReportStrategyTests.cs
       6. Edit tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs — 1 [Fact]
       7. Edit tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs — 1 [Fact]
       No files written. No agent-log entry.
```

`git status --porcelain` is empty after the preview.

## Commit hygiene

After an accepted scaffold, commit as a single change:

```powershell
git add src/backend/FinanceTracker `
        ai-artifacts/agent_log.txt
git commit -m "feat(reports): scaffold IsoWeekReportStrategy"
```

(Or accept the speckit `after_implement` hook offer, which does the same thing.)

## What this quickstart proves

- **SC-001**: the entire interaction above completes in well under 5 minutes once the developer has typed the description.
- **SC-002**: the same invocation pattern scales to `Month` and `LastNDays` without manual edits between scaffold and `dotnet test`.
- **SC-003**: the refusal demo leaves the source tree untouched, with only the log mutated.
- **SC-004**: the generated `IsoWeekReportStrategy.cs` is structurally identical (line-for-line shape) to `PeriodReportStrategy.cs`.
- **SC-005**: every invocation in this transcript (accept, refuse, preview-skipped) produced exactly the right agent-log behaviour.
- **SC-006**: the `Period` regression check at step 3 confirms behaviour preservation.
