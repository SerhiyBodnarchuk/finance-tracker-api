# Contract: File Writes

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

Defines the **exhaustive** set of file paths the `report-strategy-scaffold` skill may create, edit, or append on the accept path, and the **strict** no-op guarantee on the refuse + preview paths.

## Accept-path allow-list

The following table is closed: the skill MUST NOT touch any path outside this list. The `<Name>` placeholder is the PascalCase report type name from the developer's input (e.g., `IsoWeek`, `Month`, `LastNDays`).

| # | Path | Operation | Required? | Allowed modification |
|---|---|---|---|---|
| 1 | `src/backend/FinanceTracker/Finance.Business/Enums/ReportType.cs` | Edit | If enum value missing | Insert one new enum value into the existing `public enum ReportType { … }` block; preserve surrounding values; preserve trailing comment lines; **no other edits**. |
| 2 | `src/backend/FinanceTracker/Finance.Business/Dtos/Reports/<Name>ReportData.cs` | Create | Always | New file, exactly one top-level `public sealed record <Name>ReportData(...)` declaration. File-scoped namespace `Finance.Business.Dtos.Reports`. |
| 3 | `src/backend/FinanceTracker/Finance.Business/Services/Reports/<Name>ReportStrategy.cs` | Create | Always | New file, exactly one top-level `public sealed class <Name>ReportStrategy(...) : IReportStrategy` declaration. File-scoped namespace `Finance.Business.Services.Reports`. |
| 4 | `src/backend/FinanceTracker/Finance.Api/Program.cs` | Edit | Always | Insert exactly one line `builder.Services.AddSingleton<IReportStrategy, <Name>ReportStrategy>();` immediately after the existing `PeriodReportStrategy` registration. **No other edits.** |
| 5 | `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/<Name>ReportStrategyTests.cs` | Create | Always | New xUnit v3 test class. Uses Moq 4.20.x strict mode for repository collaborators. Mirrors `PeriodReportStrategyTests.cs` structure. |
| 6 | `src/backend/FinanceTracker/tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs` | Edit | Always | Append one new `[Fact]` named `Factory_resolves_<Name>_to_<Name>ReportStrategy` (or equivalent). Existing tests untouched. |
| 7 | `src/backend/FinanceTracker/tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs` | Edit | Always | Append one new `[Fact]` posting to `/api/reports` with the new `type` and asserting HTTP 200 + `type` echo. Existing tests untouched. |
| 8 | `ai-artifacts/agent_log.txt` | Append | Always | One block per `contracts/agent-log-entry.md` schema. |

Operations not on this list — **including but not limited to** the following — are contract violations and MUST be treated as a programming error in the skill itself:

- Editing any other file in `Finance.Api`, `Finance.Business`, or `Finance.Data`.
- Creating any file outside `Finance.Business/Dtos/Reports/`, `Finance.Business/Services/Reports/`, or the two test folders named in rows 5–7.
- Touching `Finance.Data` for any reason.
- Modifying `PeriodReportStrategy.cs`, `PeriodReportData.cs`, `PeriodReportStrategyTests.cs`, or any file related to a report type other than `<Name>`.
- Modifying `README.md`, `CLAUDE.md`, the constitution, or anything in `.specify/`, `.claude/`, or `ai-artifacts/Specifications/`.
- Running `git add`, `git commit`, `git restore`, or any state-changing `git` command. (`git status --porcelain` for dirty-tree detection per research.md R-006 is the only allowed read.)
- Running `dotnet new`, `dotnet add reference`, `dotnet add package`, or anything that touches `.csproj` files.

## Refuse-path no-op guarantee

When any refusal code fires (per `contracts/skill-input-output.md` §Refusal codes), the skill MUST:

1. Make zero edits to any file in the working tree.
2. Create zero new files anywhere in the working tree.
3. Make at most **one** mutation: appending one block to `ai-artifacts/agent_log.txt` with `decision: rejected` (per FR-012). This is the only on-disk side effect permitted on the refuse path.

**Testable invariant**: after a refusal, running `git diff --stat` and `git status --porcelain --untracked-files=all` against the pre-invocation `HEAD` shows only `ai-artifacts/agent_log.txt` as changed, and only with an append (no deletions, no other paths). This is enforceable by tests of the skill itself (a separate exercise, not in scope for this feature).

## Preview-path no-op guarantee

When `--preview` is set, the skill MUST:

1. Make zero edits, zero creates, zero appends — `git status --porcelain --untracked-files=all` MUST be empty after the run.
2. Print the planned writes (paths only) so the developer can compare against this contract before re-invoking without `--preview`.

## Diff-allowlist rules for the edited files

For each "Edit" row above, the skill's diff MUST satisfy a precise allowlist:

### Row 1 — `ReportType.cs`

Allowed: insertion of exactly one new line inside the `public enum ReportType { … }` body, positioned to keep enum values in the same order (alphabetical within the existing precedent — current file has `Period`, `IsoWeek`; new values append before the trailing `// Month, etc.` comment).

Disallowed: rewriting existing enum values, removing the trailing comment, changing the namespace declaration, changing access modifiers, adding `[JsonConverter]` or other attributes.

### Row 4 — `Program.cs`

Allowed: insertion of exactly one line, of the exact form `builder.Services.AddSingleton<IReportStrategy, <Name>ReportStrategy>();`, immediately after `builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();` (currently `Program.cs:30`).

Disallowed: any other line addition, deletion, or rewrite. The skill MUST NOT reorder existing registrations, reformat the file, change `using` directives, change DI lifetime, or rewrite the OpenAPI/Scalar block.

### Row 6 — `ReportStrategyFactoryTests.cs`

Allowed: appending one new `[Fact]` (or `[Theory]` if the existing file's style is theory-based; the skill MUST follow whichever pattern is already there) inside the existing test class. The new test's body asserts `factory.TryGet(ReportType.<Name>)` returns a non-null `IReportStrategy` whose `Type` equals `ReportType.<Name>`.

Disallowed: modifying existing tests, modifying setup/teardown helpers, modifying `using` directives unless the new test genuinely needs a new namespace (which it should not — `Finance.Business.Enums` and the test fixtures are already in scope).

### Row 7 — `ReportsEndpointTests.cs`

Allowed: appending one new `[Fact]` at the end of the existing test class. The new test uses the existing `ApiTestFixture` to drive `POST /api/reports` with `{ "type": "<Name>", "data": { … } }` and asserts HTTP 200 plus `type == "<Name>"` in the response body.

Disallowed: modifying existing tests, modifying the fixture, changing `using` directives unless strictly necessary.

## Verification

Each of the eight rows above maps to one or more functional requirements in spec.md:

| Row | FR mapping |
|---|---|
| 1 | FR-004 |
| 2 | FR-005 |
| 3 | FR-006 |
| 4 | FR-007, R-002 |
| 5 | FR-008 |
| 6 | FR-008 (factory selection coverage), R-010 |
| 7 | FR-009 |
| 8 | FR-012 |

The "modifies no other file" guarantee maps to FR-010 ("MUST NOT modify any file in `Finance.Data`, MUST NOT modify any file in `Finance.Api` other than tests, and MUST NOT modify the `Period` report's existing files"). The skill exception to FR-010's "Finance.Api other than tests" wording — row 4's one-line `Program.cs` edit — is the only API-layer edit, and it is documented in research.md R-002 as a necessary refinement of the spec's blanket prohibition.

## Out of scope for this contract

- The semantic content of `<Name>ReportStrategy.cs` (covered in `data-model.md` §3 and `quickstart.md`).
- The agent-log entry layout (covered in `contracts/agent-log-entry.md`).
- The skill's conversation flow (covered in `contracts/skill-input-output.md`).
