# Phase 1 Data Model: `report-strategy-scaffold` Claude Skill

**Branch**: `004-report-strategy-scaffold` | **Date**: 2026-05-23

This skill has no runtime database. The "data model" is the set of structured artifacts the skill reads, produces, or mutates, plus the contracts those artifacts satisfy. Four entities, in order of the skill's flow:

1. **Skill Manifest** — the `SKILL.md` file that defines the skill itself.
2. **Report Type Input** — the structured interpretation of the developer's free-text invocation.
3. **Generated Artifact Set** — the bundle of file edits the skill emits on the accept path.
4. **Agent Log Entry** — the appended `ai-artifacts/agent_log.txt` record (accept or reject).

---

## 1. Skill Manifest

**Storage**: `.claude/skills/report-strategy-scaffold/SKILL.md` (Markdown with YAML frontmatter).

**Fields** (frontmatter):

| Field | Type | Required | Validation | Notes |
|---|---|---|---|---|
| `name` | string | yes | matches `^[a-z][a-z0-9-]+$`; must equal `report-strategy-scaffold` | Used by Claude Code to surface the slash command. |
| `description` | string | yes | non-empty; ≤ 200 chars | One-line summary surfaced in the skill list. |
| `argument-hint` | string | yes | non-empty | Shown in `/`-completion. Must mention `--preview`. |
| `compatibility` | string | yes | non-empty | Names the anchor file (`Finance.Business/Services/Reports/PeriodReportStrategy.cs`). |
| `metadata.author` | string | yes | non-empty | `finance-tracker-api`. |
| `metadata.source` | string | yes | non-empty | `specs/004-report-strategy-scaffold/spec.md`. |
| `user-invocable` | bool | yes | `true` | FR-001, FR-002. |
| `disable-model-invocation` | bool | yes | `false` | FR-002. |

**Body**: the LLM prompt that drives the skill's behaviour at invocation time. Must reference:
- The accept/refuse decision flow from research.md (R-005).
- The file-write contract from `contracts/file-writes.md`.
- The agent-log entry shape from `contracts/agent-log-entry.md`.
- The dry-run flag (FR-017).

**Lifecycle**:

- Created once in this feature.
- Never overwritten by the skill itself (the skill does not modify itself).
- Updated only via `/speckit-plan` re-runs or manual edits to this feature's plan.

---

## 2. Report Type Input

**Storage**: ephemeral — the free-text following `/report-strategy-scaffold` in a Claude Code session, plus the conversational follow-ups the skill prompts for when fields are missing.

**Fields**:

| Field | Type | Required | Validation | Default |
|---|---|---|---|---|
| `Name` | PascalCase identifier | yes | Normalisable to a valid C# identifier; matches `^[A-Z][A-Za-z0-9]+$` after normalisation; not already registered as a strategy (per R-001). | — |
| `PayloadFields` | ordered list of `(name: string, type: PrimitiveTypeName, required: bool, description: string)` | yes | At least one field; each field's `type` is one of `DateOnly`, `string`, `int`, `decimal`; `name` is camelCase or PascalCase, normalised to PascalCase for the record. | — |
| `RangeRule` | string (natural-language description of how `PayloadFields` resolves to `[start, end]`) | yes | Reducible to exactly one inclusive date range per request (per FR-014(c)); marked `RangeRule.Triviality = trivial \| non-trivial` per R-004. | — |
| `PeriodDescriptorFormat` | format string | yes | A literal format like `"yyyy-Www"` (IsoWeek) or `"yyyy-MM"` (Month) or `"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}"` (Period-equivalent). | — |
| `ValidationRules` | list of free-text rules | no | Each rule must be expressible as a `ValidationError` thrown via `ReportValidationException` against `PayloadFields`. | empty (only the structural payload-deserialization check) |
| `IsPreview` | bool | no | `true` ⇒ no writes, no agent-log entry. | `false` |

**Derived**:

- `EnumValueAlreadyExists` (bool) — read from `Finance.Business/Enums/ReportType.cs`. Determines whether the skill must add the enum value or only check the value is present.
- `StrategyAlreadyExists` (bool) — read by scanning `Finance.Business/Services/Reports/*ReportStrategy.cs` for a class whose `Type` arrow expression equals `ReportType.<Name>`. If `true`, the skill refuses with `R1-strategy-exists`.

**State transitions**:

1. *Empty* (skill invoked with no args) → skill prompts for missing fields → *Populated*.
2. *Populated* → analysis → either *Accept* (writes proceed) or *Refuse* (no writes, refusal code emitted).
3. *Populated* + `IsPreview=true` → *Preview Accept* (skill prints planned writes, no file mutations).

---

## 3. Generated Artifact Set

**Storage**: a fixed set of paths inside the existing source/test trees plus one append to `ai-artifacts/agent_log.txt`. The exact path list is in `contracts/file-writes.md`. Here is the structural view.

**Components** (per accept-path invocation):

| Component | Path | Mutation | Notes |
|---|---|---|---|
| Enum value | `Finance.Business/Enums/ReportType.cs` | Edit (add one line — if absent) | Skipped if `EnumValueAlreadyExists` is `true`. |
| Data DTO | `Finance.Business/Dtos/Reports/<Name>ReportData.cs` | Create | `public sealed record <Name>ReportData(...)` matching `PayloadFields`. |
| Strategy | `Finance.Business/Services/Reports/<Name>ReportStrategy.cs` | Create | `public sealed class <Name>ReportStrategy(...) : IReportStrategy` — mirrors `PeriodReportStrategy` structure (R-008). |
| DI registration | `Finance.Api/Program.cs` | Edit (add one line) | `builder.Services.AddSingleton<IReportStrategy, <Name>ReportStrategy>();` adjacent to the existing PeriodReportStrategy registration (R-002). |
| Strategy unit tests | `tests/Finance.Business.UnitTests/Services/Reports/<Name>ReportStrategyTests.cs` | Create | Mirrors `PeriodReportStrategyTests` (R-009). |
| Factory test | `tests/Finance.Business.UnitTests/Services/Reports/ReportStrategyFactoryTests.cs` | Edit (add one `[Fact]`) | Asserts `factory.TryGet(ReportType.<Name>)` returns non-null with matching `Type` (R-010). |
| Integration test | `tests/Finance.Api.IntegrationTests/ReportsEndpointTests.cs` | Edit (add one `[Fact]`) | Posts to `/api/reports` with the new type, asserts HTTP 200 + `type` field. |
| Log entry | `ai-artifacts/agent_log.txt` | Append | One block following the schema in `contracts/agent-log-entry.md`. |

**Invariants**:

- **Atomicity at the file-set level** — either all of the above are written (accept path) or none are (refuse path, dry-run, or pre-write cancel). Partial states are an error condition (Edge Case 7) and reported in the final user message.
- **No other paths touched** — the skill MUST NOT modify any file not listed in this table. `Finance.Data` is entirely off-limits. Other report strategies' files are off-limits. Other test files are off-limits.
- **Test-skip discipline** — if `RangeRule.Triviality == non-trivial`, the resolve-to-range section of `<Name>ReportStrategy.cs` contains a `// TODO:` marker and the corresponding `<Name>ReportStrategyTests.cs` `[Fact]` carries `[Fact(Skip = "Resolution rule pending — see …")]`. The skill reports skipped facts by name in its final summary (FR-013).

---

## 4. Agent Log Entry

**Storage**: appended to `ai-artifacts/agent_log.txt` (existing file). One block per skill invocation that proceeds past arg-parsing (so: not appended in dry-run; appended for both accept and refuse paths in real runs).

**Fields** (per `contracts/agent-log-entry.md`):

| Field | Type | Required | Notes |
|---|---|---|---|
| `timestamp` | ISO-8601 local datetime | yes | `yyyy-MM-ddTHH:mm:ssK`. |
| `tool` | string | yes | E.g., `Claude Code (skill: report-strategy-scaffold)`. |
| `prompt` | quoted free-text | yes | The verbatim developer input following `/report-strategy-scaffold`. |
| `suggestion` | one-line summary | yes | On accept: report type name + list of changed file paths. On refuse: refusal code + violated rule. |
| `decision` | enum: `accepted \| rejected \| needs-revision` | yes | `needs-revision` for accept paths where `dotnet build` or `dotnet test` failed. |
| `reason` | free-text | yes | On reject: which FR-014 sub-rule fired. On needs-revision: trimmed compiler/test failure excerpt (per R-007). On accept: "build green, tests green" or similar. |

**Lifecycle**:

- Append-only; never edited or removed by the skill.
- Successful and rejected invocations both produce one entry. Dry-run produces none (FR-017).
- The entry is a flat block separated from prior entries by a blank line; the format is exactly what the file already uses (see existing `ai-artifacts/agent_log.txt`).

---

## Relationships

```text
Skill Manifest  ──reads at invocation──>  Report Type Input  ──drives──>  Generated Artifact Set
                                                  │                              │
                                                  └───────append on real run─────┘
                                                                 │
                                                                 v
                                                       Agent Log Entry
```

The dotted box is the entire scope of one skill invocation. Nothing else in the repo participates; nothing else in the repo is modified.

## Out-of-model concepts

For completeness — these are intentionally **not** part of the data model and the skill does not touch them:

- `Finance.Data` entities (`Transaction`, `Category`) and their enums.
- `Finance.Business/Services/CategoryService.cs`, `TransactionService.cs`, `ReportService.cs` — services aren't strategy-shaped.
- `Finance.Api/Controllers/ReportsController.cs` — the endpoint shape is fixed by Principle II.
- `ai-artifacts/Specifications/period-report-strategy-spec.md` — out-of-date per the constitution v2.0.0 sync impact report; the skill does not regenerate or update it.
