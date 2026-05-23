---
name: "report-strategy-scaffold"
description: "Scaffold a new report type into the Finance.Business factory + strategy pipeline (enum, DTO, strategy, DI registration, unit + integration tests)."
argument-hint: "Describe the new report's name, payload fields, payload→date-range rule, and period descriptor format. Use --preview to dry-run."
compatibility: "Requires the post-feature-003 layout (Finance.Business/Services/Reports/PeriodReportStrategy.cs as the anchor file)."
metadata:
  author: "finance-tracker-api"
  source: "specs/004-report-strategy-scaffold/spec.md"
user-invocable: true
disable-model-invocation: false
---

# report-strategy-scaffold

## Overview

This skill adds a new report type to the `finance-tracker-api` project end-to-end: it edits the `ReportType` enum, creates a new `*ReportData` record, creates a new `*ReportStrategy` class that follows the parse → validate → resolve-to-range → filter → aggregate → sort pipeline of `PeriodReportStrategy`, registers the strategy in DI, adds unit and integration tests, runs `dotnet build` + `dotnet test`, and appends an entry to `ai-artifacts/agent_log.txt`. The skill refuses cleanly (no file writes) when the request would duplicate an existing strategy, require persistence, span multiple date ranges, or be run in the wrong directory.

Authoritative design documents — read them before deviating from this prompt:

- [specs/004-report-strategy-scaffold/spec.md](../../../specs/004-report-strategy-scaffold/spec.md) — feature spec (user stories, FRs, success criteria).
- [specs/004-report-strategy-scaffold/plan.md](../../../specs/004-report-strategy-scaffold/plan.md) — implementation plan with constitution check.
- [specs/004-report-strategy-scaffold/research.md](../../../specs/004-report-strategy-scaffold/research.md) — decisions R-001..R-010.
- [specs/004-report-strategy-scaffold/data-model.md](../../../specs/004-report-strategy-scaffold/data-model.md) — artifact entities.
- [specs/004-report-strategy-scaffold/contracts/skill-input-output.md](../../../specs/004-report-strategy-scaffold/contracts/skill-input-output.md) — invocation + refusal codes.
- [specs/004-report-strategy-scaffold/contracts/file-writes.md](../../../specs/004-report-strategy-scaffold/contracts/file-writes.md) — exact path allow-list.
- [specs/004-report-strategy-scaffold/contracts/agent-log-entry.md](../../../specs/004-report-strategy-scaffold/contracts/agent-log-entry.md) — log entry schema.
- [.specify/memory/constitution.md](../../../.specify/memory/constitution.md) — project principles, especially I, II, IV, V.

## Input

The skill is invoked as:

```
/report-strategy-scaffold <free-text> [--preview | --dry-run]
```

You **MUST** consider the user input before proceeding (if not empty). The free-text after the command is the *Report Type Input*. Parse out:

1. **Name** — PascalCase identifier (e.g., `IsoWeek`, `Month`, `LastNDays`). Normalise spaces, hyphens, and leading digits to a valid C# identifier matching `^[A-Z][A-Za-z0-9]+$`. If you cannot normalise (empty name, all digits, all punctuation), emit refusal code **R5-name-not-normalisable** per [contracts/skill-input-output.md](../../../specs/004-report-strategy-scaffold/contracts/skill-input-output.md).
2. **PayloadFields** — ordered list of `(name, type, required, description)` triples. Each `type` MUST be one of `DateOnly`, `string`, `int`, `decimal`. Names normalise to PascalCase for the record properties.
3. **RangeRule** — natural-language description of how the payload yields a single inclusive `[start, end]` date range. Classify as **trivial** (deterministic and obvious from existing patterns — Period, IsoWeek, Month, Quarter) or **non-trivial** (culture-dependent, novel, or ambiguous — "previous N business days", "current fiscal quarter").
4. **PeriodDescriptorFormat** — the literal format string that produces the `period` field on `ReportResult` (e.g., `"yyyy-Www"`, `"yyyy-MM"`, `"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}"`).
5. **ValidationRules** — optional list of free-text rules expressible as `ValidationError`s thrown via `ReportValidationException`.
6. **IsPreview** — `true` if `--preview` or `--dry-run` appears in the input; otherwise `false`.

If any of fields 1–4 is missing from the user's input, ask **conversationally and in order** before doing any analysis or file write:

> Q1: What is the report type name (PascalCase, e.g. `IsoWeek`)?
> Q2: What are the request payload fields? Provide name, type, required flag for each.
> Q3: How does the payload resolve to a single inclusive date range `[start, end]`?
> Q4: What is the `period` descriptor format string?

Ask only the questions whose answers are missing. Once the input is complete, proceed to **Analysis**. Do not write any file before all required fields are present.

## Analysis

Perform **all** of these before any Write/Edit tool call. The skill is accept-or-refuse-as-a-batch (per [research.md R-005](../../../specs/004-report-strategy-scaffold/research.md)) — never half-finished writes.

1. **Anchor file check.** Confirm `src/backend/FinanceTracker/Finance.Business/Services/Reports/PeriodReportStrategy.cs` exists. If absent, emit refusal code **R4-missing-anchor**.

2. **Enum-presence check.** Read `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs`. Set `EnumValueAlreadyExists = true` if the requested Name is already declared (case-sensitive — C# enums are case-sensitive); otherwise `false`. Note: enum-value-present alone is **not** a refusal trigger. The current `ReportType` enum declares `IsoWeek` without an implementing strategy; scaffolding the strategy for an existing-but-unimplemented enum value is the intended primary use case.

3. **Strategy-existence check.** Scan `src/backend/FinanceTracker/Finance.Business/Services/Reports/*ReportStrategy.cs` for a class with `Type => ReportType.<Name>` (an arrow expression body returning the requested type). If found, emit refusal code **R1-strategy-exists**, naming the existing file path.

4. **Persistence-or-caching check.** Examine `RangeRule` and `ValidationRules` for any mention of persisting, caching, storing, or remembering between requests. If present, emit refusal code **R2-persistence-required** (constitution Principle II: reports are ad-hoc and MUST NOT be persisted).

5. **Multi-range check.** Examine `RangeRule`. If it implies more than one date range per request (diff between windows, union of arbitrary ranges, multi-period comparison), emit refusal code **R3-multi-range-payload**.

6. **Name normalisability check.** If Name cannot be normalised to a PascalCase C# identifier (`^[A-Z][A-Za-z0-9]+$`), emit refusal code **R5-name-not-normalisable**.

7. **Dirty-tree check** (skip if `IsPreview`). Shell out to `git status --porcelain` (the **only** allowed `git` command). If output is non-empty, list the dirty files to the developer and ask explicitly: "The working tree has uncommitted changes. Proceed anyway?" If the developer declines or is silent, emit refusal code **R6-dirty-tree-declined**.

8. **Triviality classification.** Mark `RangeRule.Triviality` as `trivial` or `non-trivial` (per step 3 of Input). This drives whether the strategy's resolve-to-range method body is real code or a `TODO` marker plus a `[Fact(Skip = …)]` test.

When all checks pass, you are on the **accept path** (or **preview path** if `IsPreview` is true). When any check fails, you are on the **refuse path**.

## Plan summary (always emit)

Before any writes (and as the entire output on the preview path), print a plan summary in this exact shape, replacing `<Name>` with the normalised report type name:

```
Plan:
  1. <Add | Skip> ReportType.<Name> in Finance.Business/Enums/ReportType.cs (<reason>)
  2. Create Finance.Business/Dtos/Reports/<Name>ReportData.cs (<field list>)
  3. Create Finance.Business/Services/Reports/<Name>ReportStrategy.cs (<trivial | non-trivial — TODO in resolve-to-range>)
  4. Edit Finance.Api/Program.cs — add one DI line after PeriodReportStrategy registration
  5. Create tests/Finance.Business.UnitTests/Services/Reports/<Name>ReportStrategyTests.cs
  6. Edit tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs — one new [Fact]
  7. Edit tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs — one new [Fact]
  (preview: no writes; or accept: applying now…)
```

On the preview path, end with the literal line `No files written. No agent-log entry.` and exit.

## Accept path

Perform the following seven file operations in order. Each operation has a strict diff allow-list defined in [contracts/file-writes.md](../../../specs/004-report-strategy-scaffold/contracts/file-writes.md). Read that contract; **do not modify anything outside the allow-list**. If you find yourself wanting to touch any other file, stop and re-classify as refuse path with an informative error.

### Step 1: enum value

Edit `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs`. Insert one new line inside the `public enum ReportType { … }` body, positioned to keep existing values in their current order and inserted **before** the trailing `// Month, etc. — added when those report types are scheduled.` comment. Preserve namespace, modifiers, attributes, and the trailing comment line. **Skip this step entirely if `EnumValueAlreadyExists` is `true`** (the value is already declared — leave the file untouched and report "skipped" in the summary).

Allowed modification: exactly one new enum-value line. Disallowed: rewriting existing values; removing the trailing comment; adding `[JsonConverter]` or any attribute; changing access modifier.

### Step 2: data DTO

Create `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/<Name>ReportData.cs`. The file MUST follow this template exactly:

```csharp
namespace Finance.Business.Dtos.Reports;

public sealed record <Name>ReportData(
    <Type1> <Property1>,
    <Type2> <Property2>);
```

- File-scoped namespace `Finance.Business.Dtos.Reports`.
- Single top-level `public sealed record` with positional parameters.
- One property per `PayloadField`, in input order.
- No XML doc comments, no `[JsonPropertyName]` attributes (the centralised `JsonSerializationOptions` handles naming).
- No additional types in the file.

### Step 3: strategy class

Create `src/backend/FinanceTracker/Finance.Business/Services/Reports/<Name>ReportStrategy.cs`. Mirror the structure of `PeriodReportStrategy.cs` step-for-step. The five-step shape (parse → validate → resolve-to-range → filter → aggregate → sort) is mandatory; deviation defeats the entire point of the skill. Template skeleton:

```csharp
using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Validation;
using Finance.Data.Models;
using Finance.Data.Repositories;

namespace Finance.Business.Services.Reports;

public sealed class <Name>ReportStrategy(
    ITransactionRepository transactions,
    ICategoryRepository categories)
    : IReportStrategy
{
    public ReportType Type => ReportType.<Name>;

    public ReportResult Generate(ReportRequest request)
    {
        var data = ParseAndValidate(request);

        // Step 3a: resolve-to-range
        var (rangeStart, rangeEnd) = ResolveRange(data);

        // Step 3b: filter
        var inWindow = new List<Transaction>();
        foreach (var t in transactions.GetAll())
        {
            if (t.Timestamp >= rangeStart && t.Timestamp <= rangeEnd)
                inWindow.Add(t);
        }

        // Step 3c: aggregate totals
        var incomeTotal = 0m;
        var expenseTotal = 0m;
        foreach (var t in inWindow)
        {
            if (t.Type == TransactionType.Income) incomeTotal  += t.Amount;
            else                                  expenseTotal += t.Amount;
        }
        var netTotal = incomeTotal - expenseTotal;

        // Step 3d: per-category breakdown
        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var breakdownTotals = new Dictionary<int, decimal>();
        foreach (var t in inWindow)
        {
            var signed = t.Type == TransactionType.Income ? t.Amount : -t.Amount;
            foreach (var categoryId in t.CategoryIds)
            {
                breakdownTotals[categoryId] = breakdownTotals.TryGetValue(categoryId, out var current)
                    ? current + signed
                    : signed;
            }
        }

        // Step 3e: sort breakdown
        var breakdown = breakdownTotals
            .Select(kvp => new CategoryBreakdownItem(categoryNames[kvp.Key], kvp.Value))
            .OrderBy(item => item.Total < 0m)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToList();

        var period = <PeriodDescriptorExpression>;

        return new ReportResult(
            ReportType.<Name>,
            period,
            incomeTotal,
            expenseTotal,
            netTotal,
            breakdown);
    }

    private static (DateTime Start, DateTime End) ResolveRange(<Name>ReportData data)
    {
        <!-- trivial: real code that turns the payload into [Start 00:00:00, End 23:59:59] -->
        <!-- non-trivial: throw new NotImplementedException("TODO: resolve <Name> payload to date range"); -->
    }

    private static <Name>ReportData ParseAndValidate(ReportRequest request)
    {
        <Name>ReportData? data;
        try
        {
            data = request.Data.Deserialize<<Name>ReportData>(JsonSerializationOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", $"Invalid <name> report data payload: {ex.Message}")
            });
        }

        if (data is null)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", "<Name> report data payload is required.")
            });
        }

        var errors = new List<ValidationError>();
        <!-- per-field required checks and payload-specific validation rules -->

        if (errors.Count > 0)
            throw new ReportValidationException(errors);

        return data;
    }
}
```

Trivial-rule example (`IsoWeek`): `ResolveRange` parses `data.Week` (`"yyyy-Www"`) via `System.Globalization.ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)`, sets `Start = monday.Date`, `End = monday.AddDays(6).Date + new TimeSpan(23, 59, 59)`. Real code, no TODO.

Non-trivial-rule example (`LastNBusinessDays`): `ResolveRange` throws `NotImplementedException` with a clear message. The corresponding unit test gets `[Fact(Skip = "Resolution rule pending: clarify business-day definition")]`.

### Step 4: DI registration

Edit `src/backend/FinanceTracker/Finance.Api/Program.cs`. Insert exactly one line:

```csharp
builder.Services.AddSingleton<IReportStrategy, <Name>ReportStrategy>();
```

immediately after the existing `builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();` line (currently at line 30). Preserve indentation.

**Allowed**: exactly one line addition at that one position. **Disallowed**: any other line change, reformat, `using` directive change, reordering of registrations, OpenAPI/Scalar block edits, lifetime changes, or comment additions. If you find yourself touching anything else, you have misunderstood the contract — stop and re-read [contracts/file-writes.md §Diff-allowlist rules / Row 4](../../../specs/004-report-strategy-scaffold/contracts/file-writes.md).

### Step 5: strategy unit tests

Create `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/<Name>ReportStrategyTests.cs`. Mirror the structure of `PeriodReportStrategyTests.cs`:

- xUnit v3 with `using Xunit;`. **No** `using FluentAssertions;` — assertions are `Xunit.Assert` only.
- Moq 4.20.x with strict-mode mocks: `var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);`
- Class-per-strategy. `[Fact]` per scenario. Arrange / Act / Assert sections separated by blank lines.
- At minimum, emit these `[Fact]`s:
  1. **Happy path** — fixed input transactions, asserts the expected `ReportResult` fields (`type`, `period`, `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown` sorted income-first then alphabetical).
  2. **Invalid payload** — `request.Data` is malformed JSON; asserts `ReportValidationException` is thrown and contains a `ValidationError` for `data`.
  3. **Missing required field** — one required payload field is missing; asserts `ReportValidationException` with a `ValidationError` whose `Field` is `data.<missingField>`.
  4. **Payload-shape edge case** specific to the new type (e.g., malformed `Week` string for IsoWeek; `start > end` for date-range types).
  5. **Period descriptor formatting** — asserts the `period` field of the result matches `PeriodDescriptorFormat` exactly.

For non-trivial RangeRule, `[Fact]` 1 (happy path) MUST carry `[Fact(Skip = "<reason>")]` so `dotnet test` stays green.

### Step 6: factory test extension

Edit `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs`. Append exactly one new `[Fact]` inside the existing test class, named `Factory_resolves_<Name>_to_<Name>ReportStrategy` (or follow the existing file's naming convention if different). The test body asserts:

```csharp
var strategy = factory.TryGet(ReportType.<Name>);

Assert.NotNull(strategy);
Assert.Equal(ReportType.<Name>, strategy!.Type);
Assert.IsType<<Name>ReportStrategy>(strategy);
```

Use the existing test fixture / setup helpers in the file. Do **not** modify existing tests, setup methods, or `using` directives unless strictly necessary.

### Step 7: integration test extension

Edit `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs`. Append exactly one new `[Fact]` at the end of the existing test class. The test:

- Uses the existing `ApiTestFixture` constructor injection.
- POSTs `{ "type": "<Name>", "data": { <example payload> } }` to `/api/reports`.
- Asserts HTTP 200 and that the response body's `type` field equals `"<Name>"`.

Use a payload value that the seeded in-memory transactions would actually produce non-empty results for, when possible. For non-trivial RangeRule scaffolds where `ResolveRange` throws `NotImplementedException`, the integration test would surface a 500 — in that case, mark this `[Fact]` with `[Fact(Skip = "<reason>")]` to keep the test run green and report the skip in the final summary.

### Step 8: build + test verification

Run (from the repo root):

```powershell
cd src/backend/FinanceTracker
dotnet build
```

Capture exit code and stdout/stderr.

If `dotnet build` exit code is non-zero, find the first line matching `error CS\d+:` or `error MSB\d+:`. Print that line plus the 3 surrounding context lines to the developer. Do **not** print the full log.

If `dotnet build` succeeded, run:

```powershell
dotnet test
```

Capture exit code and last ~60 lines. If exit code is non-zero, find the first line matching `Failed `, `[xUnit.net …]`, or `Test Run Failed`. Print that line plus the 3 surrounding context lines. Do **not** print the full log.

### Step 9: agent log append

Append one entry to `ai-artifacts/agent_log.txt`. **Match the existing file's prose-block format** (open the file and read the last entry as a template). The required logical fields are:

- Timestamp — ISO-8601 local datetime.
- Model / tool — `Claude Code (skill: report-strategy-scaffold)`.
- Prompt — the verbatim developer input following `/report-strategy-scaffold` (multi-line preserved or joined with ` \n ` if the existing format expects single-line).
- AI suggestion — one-line summary: `Scaffold <Name>ReportStrategy: add enum, DTO, strategy, DI line, 2 tests` (or `Scaffold <Name>ReportStrategy: writes applied, build/test failed — see reason` on the needs-revision path).
- Decision — `accepted` if build + tests are green; `needs-revision` if either failed.
- Reason — `Build green, tests green` (optionally `(N skipped: <names>)`) on accept; trimmed failure excerpt on needs-revision.
- Files — comma-separated list of repo-relative paths the skill modified. The `ai-artifacts/agent_log.txt` file itself is **not** in this list.

The block separator is the `=========================================================================` line the file already uses. Do not introduce a new separator style. The schema in [contracts/agent-log-entry.md](../../../specs/004-report-strategy-scaffold/contracts/agent-log-entry.md) defines the logical fields; the **literal layout** follows the file's existing prose style — read the last entry before appending and match it.

### Step 10: final summary

Emit a developer-facing summary in this exact shape:

```
Writes applied (N files).
Running dotnet build … <success | failure>.
Running dotnet test  … <success | failure> (M passed, K skipped).
Appended ai-artifacts/agent_log.txt entry [decision: <accepted | needs-revision>].

Summary:
- New report type: <Name>
- Changed files:
    <path 1>
    <path 2>
    …
- Build: <green | red>
- Tests: <green (M pass) | green (M pass, K skipped: <names>) | red — <first failure>>
- <Ready to commit. | Follow-ups: <skipped tests, TODO markers, etc.>>
```

If any `[Fact(Skip = …)]` tests were emitted, name them explicitly in the Follow-ups list (FR-013).

## Refuse path

When any Analysis check fires, do **not** make any production-code edits. Emit:

1. The refusal code and explanation:

   ```
   Refuse: <R1-strategy-exists | R2-persistence-required | R3-multi-range-payload | R4-missing-anchor | R5-name-not-normalisable | R6-dirty-tree-declined>
   <one-sentence explanation citing the rule violated>
   Files changed: 0
   ```

2. **One** append to `ai-artifacts/agent_log.txt`, matching the existing file's prose-block format, with the logical fields:

   - Timestamp.
   - Model / tool — `Claude Code (skill: report-strategy-scaffold)`.
   - Prompt — verbatim developer input.
   - AI suggestion — `Refuse: <code> — <one-clause rationale>` (e.g., `Refuse: R1-strategy-exists — PeriodReportStrategy already at Finance.Business/Services/Reports/PeriodReportStrategy.cs`).
   - Decision — `rejected`.
   - Reason — FR-014 sub-rule or constitution principle violated.
   - Files — `0`.

3. The final user message ends with: `Appended ai-artifacts/agent_log.txt entry [decision: rejected].`

No `dotnet build` or `dotnet test`. No edits to anything other than the agent log. After the refusal, `git status --porcelain` MUST show only `ai-artifacts/agent_log.txt` as modified — nothing else.

Do **not** invite the developer to confirm or override the refusal. Refusals are decisive.

## Refusal-code detection rules (reference)

| Code | Trigger |
|---|---|
| `R1-strategy-exists` | Analysis step 3 found a class with `Type => ReportType.<Name>`. Print the existing file path. |
| `R2-persistence-required` | Analysis step 4 detected persistence/caching language in RangeRule or ValidationRules. Cite constitution Principle II. |
| `R3-multi-range-payload` | Analysis step 5 found > 1 inclusive date range per request. |
| `R4-missing-anchor` | Analysis step 1 could not find `Finance.Business/Services/Reports/PeriodReportStrategy.cs`. |
| `R5-name-not-normalisable` | Analysis step 6: Name fails `^[A-Z][A-Za-z0-9]+$` after normalisation. |
| `R6-dirty-tree-declined` | Analysis step 7: working tree dirty and developer declined to proceed. |

## Preview path

When `IsPreview` is `true`:

1. Run all Analysis checks **except** the dirty-tree check (it's irrelevant when no writes happen).
2. If any check fires, emit the matching refusal output **but** do not append to `ai-artifacts/agent_log.txt` (FR-017 — the preview path appends nothing).
3. Otherwise, print the Plan summary (above) **without** writing any file.
4. End with the literal line: `No files written. No agent-log entry.`

After a preview, `git status --porcelain` MUST be empty.

## Constraints (binding)

- The eight-row allow-list in [contracts/file-writes.md](../../../specs/004-report-strategy-scaffold/contracts/file-writes.md) is closed. You MUST NOT create or modify any other path.
- `Finance.Data` is entirely off-limits. The Data layer does not participate in reports.
- The only API-layer edit is the one DI line in `Program.cs` (Step 4). No controller edits, no Infrastructure edits, no Validator edits.
- `PeriodReportStrategy.cs`, `PeriodReportData.cs`, `PeriodReportStrategyTests.cs` MUST be left untouched.
- The only allowed `git` command is `git status --porcelain` (read-only). No `git add`, `git commit`, `git restore`, `git push`, or any state change. The speckit `after_implement` hook handles commits — you do not.
- No new NuGet packages, no `dotnet add`, no `.csproj` edits.
- Match existing code style: file-scoped namespaces, `public sealed record`/`public sealed class`, primary constructor DI, no XML doc comments, no inline comments unless the *why* is genuinely non-obvious (per CLAUDE.md's "default to writing no comments" rule).
- Match existing test style: xUnit v3 (`Xunit.Assert`), Moq 4.20.x strict mode, no FluentAssertions.
- All projects target `net10.0`. Do not introduce code that requires earlier TFMs.

## Conversation contract

- Ask only the four Input questions above, and only those whose answers are missing.
- Ask the dirty-tree confirmation question only when `git status --porcelain` is non-empty.
- Never ask the developer to confirm a refusal or an accept — both proceed without confirmation once analysis decides.

## Out of scope

This skill does not:

- Rename or remove existing report types.
- Edit the OpenAPI/Scalar configuration.
- Modify `ReportsController.cs` or any other controller — the controller is type-agnostic by design (Principle II).
- Update documentation (`README.md`, `CLAUDE.md`, `ai-artifacts/Specifications/*`).
- Run `git` state-changing commands.
- Install or upgrade NuGet packages.

If the developer's request implies any of the above, refuse with a short explanation and suggest a manual approach.