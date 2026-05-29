# finance-tracker-api Constitution — Amendment History

*Append-only (append at end — oldest entry first, newest last). Never read during normal development work. Used only when filing a new amendment via `/speckit-constitution`.*

---

Version change: 1.0.0 -> 2.0.0
Bump rationale: Ratifies the six drifts captured in
specs/001-domain-entities-dtos/plan.md "Complexity Tracking". Four
NON-NEGOTIABLE principles are redefined in backwards-incompatible ways
(I DTO ownership; II envelope + ReportResult shape; III identifier type +
multi-category + USD-only; IV test layout + runner version + assertion
library), which forces a MAJOR bump per the Governance versioning policy.

Drifts ratified:
- #1 (Principle I) DTOs moved from Finance.Api to Finance.Business. The API
  layer now reuses Business DTOs directly and never sees a domain entity —
  mapping from Transaction/Category to TransactionResponse/CategoryResponse
  happens in Business via the new TransactionMapper / CategoryMapper. The
  centralized JsonSerializationOptions also lives in Business.
- #2 (Principle II) Report request envelope changed from
  { type: <string>, parameters: <object> } to
  { type: <ReportType-enum>, data: <per-type DTO carried as JsonElement> }.
  Per-ReportType data shapes introduced: PeriodReportData { start, end } and
  IsoWeekReportData { week }. Unknown enum values fail at request binding,
  not in the factory.
- #3 (Principle II) ReportResult collapsed to a summary envelope:
  { type, period (string descriptor), incomeTotal, expenseTotal, netTotal,
    categoryBreakdown }. No transactions array, no currency field.
  CategoryBreakdownItem reduced to { category, total (signed) } — no
  transactionType field, no transaction count. Sort order: income-first
  (positive total), then expense-side (negative), alphabetical within each.
  Multi-category attribution: a transaction tagged with N categories
  contributes its full signed amount to each of the N breakdown items
  (the sum of breakdown totals may exceed netTotal in absolute value when
  multi-tagged transactions exist — by design).
- #4 (Principle III) Identifiers changed from Guid to int on both Transaction
  and Category. Seed data uses deterministic integer IDs.
- #5 (Principle III) Transactions are now multi-category:
  Transaction.CategoryIds : IReadOnlyList<int> (non-empty). Every attached
  category's CategoryType must be compatible with the transaction's
  TransactionType (the single-category bullet is replaced). Also clarified:
  Transaction.Timestamp is full DateTime (year-second), and all amounts are
  USD with no currency field anywhere.
- #6 (Principle IV) Tests live under src/backend/FinanceTracker/tests/ across
  four projects: Finance.Data.UnitTests, Finance.Business.UnitTests,
  Finance.Api.UnitTests, Finance.Api.IntegrationTests — not a single
  Finance.Tests project. Runner pinned to xUnit v3 (xunit.v3, OutputType=Exe).
  Assertion library: built-in Xunit.Assert API. FluentAssertions is NOT used.
  Older ai-artifact specs still phrase required tests in FluentAssertions
  style; those phrasings translate one-to-one to Xunit.Assert equivalents
  at implementation time.

Modified principles:
- I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) — drift #1
- II. Report Factory + Strategy Pattern (NON-NEGOTIABLE) — drifts #2, #3
- III. Seeded In-Memory Determinism (NON-NEGOTIABLE in spirit; kept as-is
  re: NON-NEGOTIABLE label from v1.0.0 — actually labelled without the tag in
  v1.0.0; re-read principle for current status) — drifts #4, #5
- IV. Test-First with xUnit v3 — drift #6 (title also changed: previously
  "Test-First with xUnit + FluentAssertions")

Renamed principles:
- IV. "Test-First with xUnit + FluentAssertions" -> "Test-First with xUnit v3"

Added sections: none.
Removed sections: none.

Templates requiring updates:
- .specify/templates/plan-template.md - no edit required.
- .specify/templates/spec-template.md - no edit required.
- .specify/templates/tasks-template.md - no edit required.
- .specify/templates/checklist-template.md - no edit required.
- .specify/templates/constitution-template.md - no edit required.

Runtime guidance docs:
- README.md - already aligned.
- CLAUDE.md - already aligned.
- specs/001-domain-entities-dtos/{spec,plan,research,data-model,contracts/,quickstart}.md - already aligned.

Deferred items / known out-of-sync documents (intentionally not edited per
user direction):
- ai-artifacts/Specifications/in-memory-repository-spec.md - still describes
  Guid identifiers, DateOnly transaction dates, single-category transactions,
  a currency field, and FluentAssertions-phrased required tests.
- ai-artifacts/Specifications/period-report-strategy-spec.md - still
  describes the old { type:string, parameters:object } envelope, the old
  ReportResult shape, and FluentAssertions-phrased required tests.
- .specify/extensions/git/scripts/powershell/initialize-repo.ps1 - has a
  Unicode encoding bug under PowerShell 5.1 on Windows; upstream extension
  issue; before_constitution hook skipped.

----------------------------------------------------------------------
Version change: 2.0.0 -> 2.0.1
Bump rationale: PATCH. Sweeps three leftover wording inconsistencies in the
"Technology & Scope Constraints" section that the v2.0.0 amendment did not
propagate from the principle text. No principle is added, removed, or
redefined.
- "Testing: xUnit + FluentAssertions (see Principle IV)." replaced with
  "Testing: xUnit v3 with the built-in Xunit.Assert API (see Principle IV).
  FluentAssertions is NOT used."
- "All projects (Finance.Api, Finance.Business, Finance.Data, Finance.Tests)
  MUST target net10.0" replaced with a reference to the four test projects
  under src/backend/FinanceTracker/tests/.
- "Three production projects + one test project under
  src/backend/FinanceTracker/" replaced with explicit naming of the three
  production projects and the four test projects under tests/.

Templates / runtime docs touched:
- CLAUDE.md — removed the now-stale "Run /speckit-constitution to bump the
  constitution to v2.0.0" warning.

Deferred items: same set as v2.0.0 (unchanged by this patch).

----------------------------------------------------------------------
Version change: 2.0.1 -> 3.0.0
Bump rationale: MAJOR. Principle I (Three-Layer Architecture Boundaries,
NON-NEGOTIABLE) is redefined in a backwards-incompatible way:
- A new application service layer is introduced under
  Finance.Business/Services/ (ICategoryService, ITransactionService,
  IReportService and their implementations). Controllers MUST go through
  services and MUST NOT call repositories directly.
- Validators move from Finance.Business to
  Finance.Api/Infrastructure/Validators/. Validators in Finance.Api MUST
  depend on Business services for any cross-entity data and MUST NOT
  reference repositories directly (same access rule as controllers).
- The report system relocates under Finance.Business/Services/Reports/
  (the ReportStrategyFactory and every IReportStrategy implementation,
  plus ReportValidationException). Pattern and contracts are unchanged
  from v2.0.1; only the namespace/folder moves.
- ValidationResult and ValidationError data shapes remain in
  Finance.Business/Validation/ because they're consumed both by
  validators (in Finance.Api) and by ReportValidationException (in
  Finance.Business); moving them to API would force a Business -> Api
  reference and break dependency direction.

Drifts ratified (executed across feature 003-transactions-categories-reports):
- Drift #A (Principle I) Application service layer introduced.
- Drift #B (Principle I) Validators relocated to Finance.Api/Infrastructure/Validators/.
- Drift #C (Principle I) Reports relocated under Finance.Business/Services/Reports/.

Minor additive changes (bundled with the MAJOR bump):
- Principle IV: explicit mention of Moq (Moq 4.20.x) and
  Microsoft.AspNetCore.Mvc.Testing. Two new required coverage bullets:
  service-layer behaviour and controller-level HTTP shape.
- Technology & Scope Constraints "Testing" bullet: same library acknowledgments.

Modified principles:
- I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE) - drifts #A, #B, #C
- IV. Test-First with xUnit v3 - additive: Moq + Mvc.Testing acknowledgments,
  two new required coverage bullets

Renamed principles: none.
Added sections: none.
Removed sections: none.

Templates / runtime docs touched:
- CLAUDE.md - SPECKIT block updated to point at constitution v3.0.0;
  architecture rules and file-layout references aligned with the
  services/validators reorganization.

Deferred items / known out-of-sync documents (intentionally not edited
per user direction):
- ai-artifacts/Specifications/in-memory-repository-spec.md - still pre-amendment.
- ai-artifacts/Specifications/period-report-strategy-spec.md - still pre-amendment.
- specs/003-transactions-categories-reports/* - describe the feature design at
  the point of /speckit-plan; in-flight restructure documented in agent_log.txt.
- .specify/extensions/git/scripts/powershell/initialize-repo.ps1 - Unicode bug;
  same precedent as v2.0.0 and v2.0.1.

----------------------------------------------------------------------
Version change: 3.0.0 -> 3.0.1
Bump rationale: PATCH. Test-project wiring in Principle IV is refined to
match the running implementation after feature 005-ci-pipeline shipped a
GitHub Actions CI workflow. No principle is added, removed, or redefined.

Specifically:
- "All four MUST use xUnit v3 (xunit.v3, OutputType=Exe)" relaxes to
  "xunit.v3" only. The OutputType=Exe wording assumed pure-MTP execution;
  that path proved fragile under the .NET 10 SDK's dotnet test integration.
  The test projects now follow the standard VSTest path:
    + Microsoft.NET.Test.Sdk 17.12.0
    + xunit.runner.visualstudio 3.0.2 (the v3-compatible VSTest adapter)
    - OutputType=Exe removed
- The "Technology & Scope Constraints" Testing bullet gains an explicit
  mention of Microsoft.NET.Test.Sdk and xunit.runner.visualstudio.
- The "CI" bullet's reference to `.github/workflows/ci.yml` now points to
  an actually-present file (feature 005-ci-pipeline shipped it).

Modified principles:
- IV. Test-First with xUnit v3 — OutputType=Exe removed; VSTest adapter
  + Microsoft.NET.Test.Sdk acknowledged.

Renamed principles: none.
Added sections: none.
Removed sections: none.

Templates / runtime docs touched:
- CLAUDE.md — Commands section test-stack description updated.
- .specify/templates/*.md — no edits required.
- specs/005-ci-pipeline/* — already describe the final implementation.
- ai-artifacts/agent_log.txt — appended with this cleanup entry.

Deferred items: same set as v3.0.0 (unchanged by this patch).

----------------------------------------------------------------------
Version change: 3.0.1 -> 3.0.2
Bump rationale: PATCH. Principle V gains an explicit append-only rule for
agent_log.txt — entries MUST NOT be deleted, truncated, or rewritten. No
principle is added, removed, or redefined; the rule was always implied by
"appended", and this patch makes it normative text.

Modified principles:
- V. AI-Assisted Development Transparency — append-only constraint added
  to the agent_log.txt obligation.

Templates / runtime docs touched:
- CLAUDE.md — AI-assisted development log section gains append-only note;
  new "AI output hygiene" section added prohibiting planning/analysis docs
  outside the permitted paths.
- README.md — AI-Assisted Development Log section gains append-only note.

Deferred items: same set as 3.0.1.
