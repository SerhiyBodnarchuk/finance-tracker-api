<!--
Sync Impact Report
==================
Version change: 2.0.0 -> 2.0.1
Bump rationale (2.0.1): PATCH. Sweeps three leftover wording inconsistencies
in the "Technology & Scope Constraints" section that the v2.0.0 amendment
did not propagate from the principle text. No principle is added, removed,
or redefined.
- "Testing: xUnit + FluentAssertions (see Principle IV)." replaced with
  "Testing: xUnit v3 with the built-in Xunit.Assert API (see Principle IV).
  FluentAssertions is NOT used." — aligns with Principle IV (rewritten in
  v2.0.0) which explicitly forbids FluentAssertions.
- "All projects (Finance.Api, Finance.Business, Finance.Data, Finance.Tests)
  MUST target net10.0" replaced with a reference to the four test projects
  under src/backend/FinanceTracker/tests/ — the single `Finance.Tests`
  project was replaced by four in v2.0.0 (drift #6) but this bullet still
  referenced it.
- "Three production projects + one test project under
  src/backend/FinanceTracker/" replaced with explicit naming of the three
  production projects and the four test projects under tests/ — same
  underlying drift #6 cleanup.
Templates / runtime docs touched: CLAUDE.md (removed the now-stale
"Run /speckit-constitution to bump the constitution to v2.0.0" warning at
the bottom of the SPECKIT block; the bump it referred to is the v2.0.0
amendment, which has been in place since 2026-05-19).

----------------------------------------------------------------------
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
- .specify/templates/plan-template.md - no edit required. The Constitution
  Check section defers to this file at runtime; the per-principle gates in
  the workflow (dependency direction, factory+strategy, singleton in-memory
  with deterministic seed IDs, required test coverage areas, no out-of-scope
  tech) all read from this file directly. Verified.
- .specify/templates/spec-template.md - no edit required. The spec template
  is story / requirement-driven and does not encode principle-specific gates.
- .specify/templates/tasks-template.md - no edit required. Task categories
  are story-driven, not principle-driven.
- .specify/templates/checklist-template.md - no edit required.
- .specify/templates/constitution-template.md - no edit required.

Runtime guidance docs:
- README.md - already aligned. The Core Domain table, Reports section,
  example payloads, Business contracts table, and Tech Stack line all match
  the amended principles (DTOs in Business; int IDs; full DateTime;
  multi-category transactions; typed report envelope; summary ReportResult;
  xUnit v3 / no FluentAssertions).
- CLAUDE.md - already aligned. The "Architecture rules", "Report system",
  "Domain rules worth knowing", "Current state vs. spec", "Commands", and
  SPECKIT block sections all match the amended principles. The top-of-file
  divergence note about the two ai-artifacts/Specifications/*.md files is
  retained because those specs are still out of date (see below).
- specs/001-domain-entities-dtos/spec.md, plan.md, research.md,
  data-model.md, contracts/, quickstart.md - already aligned. The plan's
  Constitution Check should re-evaluate to all-PASS now that this amendment
  ratifies the drifts.

Deferred items / known out-of-sync documents (intentionally not edited per
user direction):
- ai-artifacts/Specifications/in-memory-repository-spec.md - still describes
  Guid identifiers, DateOnly transaction dates, single-category transactions,
  a currency field, and FluentAssertions-phrased required tests. The user has
  directed that this file stays untouched until the in-memory repository
  feature is scheduled, at which point it will be rewritten as part of that
  feature slice.
- ai-artifacts/Specifications/period-report-strategy-spec.md - still
  describes the old { type:string, parameters:object } envelope, the old
  ReportResult shape (with transactions array, incomeTotal/expenseTotal/
  netTotal alongside the transactions, per-(category, direction) breakdown
  with transactionCount), and FluentAssertions-phrased required tests. Same
  user-directed deferral applies.
- .specify/extensions/git/scripts/powershell/initialize-repo.ps1 - has a
  Unicode encoding bug (checkmark character in Write-Host) that causes the
  before_constitution hook to error out under PowerShell 5.1 on Windows. The
  hook is conceptually a no-op for an existing repo (just git init), and the
  amendment proceeded without it. The bug is upstream in the speckit
  extension, not in this project's code.
-->

# finance-tracker-api Constitution

## Core Principles

### I. Three-Layer Architecture Boundaries (NON-NEGOTIABLE)

The solution is split into exactly three projects with a strict one-way dependency
graph: `Finance.Api -> Finance.Business -> Finance.Data`. Reverse dependencies
(`Finance.Data` referencing `Finance.Business` or `Finance.Api`, or
`Finance.Business` referencing `Finance.Api`) MUST NOT be introduced.

Each layer owns a disjoint set of responsibilities:

- `Finance.Data` owns domain entities (`Transaction`, `Category` — both
  records) and their related enums (`TransactionType`, `CategoryType` — enums,
  not standalone entities), repository interfaces, and seeded in-memory
  repository implementations. It MUST NOT contain aggregation logic, HTTP
  concerns, or DTOs.
- `Finance.Business` owns **all DTOs** — both the HTTP request/response shapes
  (`TransactionCreateRequest`, `TransactionResponse`, `CategoryCreateRequest`,
  `CategoryResponse`) and the report contracts (`ReportRequest`,
  `PeriodReportData`, `IsoWeekReportData`, `ReportResult`,
  `CategoryBreakdownItem`) — plus the `ReportType` enum, the trivial 1:1
  mappers between domain entities and DTOs (`TransactionMapper`,
  `CategoryMapper`), the centralized `JsonSerializationOptions`, the report
  factory and strategies, validation, aggregation, and (later) MCP
  abstractions. It MUST NOT reference ASP.NET Core types or HTTP primitives.
- `Finance.Api` owns controllers / minimal-API endpoints, model binding
  (reusing the Business-layer DTOs directly), status-code mapping, OpenAPI +
  Scalar wiring, and DI composition. Controllers MUST delegate aggregation to
  a Business-layer strategy and MUST NOT touch repositories directly.
  API-specific models MUST NOT be introduced here unless there is a concrete
  need beyond the Business DTOs (none for the MVP). **The API layer never
  sees a domain entity** — the Business layer maps `Transaction`/`Category`
  to the corresponding response DTO before any data crosses the boundary.

**Rationale**: This separation is the spine of the project and the reason the
MVP can stay testable without a database. Any aggregation logic in the API
layer, or any repository access from a controller, breaks both the MCP replay
story and the test pyramid. Centralising DTOs in the Business layer (rather
than duplicating them in API) keeps "what the application accepts and emits"
defined in one place and lets the API stay a thin transport adapter; the
explicit "API never sees a domain entity" rule is what makes that boundary
testable in isolation.

### II. Report Factory + Strategy Pattern (NON-NEGOTIABLE)

All reports are produced through a single endpoint `POST /api/reports` that
accepts the typed envelope
`{ "type": <ReportType-enum>, "data": <per-type payload> }`. The endpoint MUST
resolve the `ReportType` enum value through a `ReportStrategyFactory` to an
`IReportStrategy` implementation, run it, and return a common `ReportResult`.
Each strategy MUST own its full parse → validate → aggregate → return
pipeline; the controller MUST NOT branch on `type`, parse `data`, or perform
aggregation. The `data` payload is delivered as a `System.Text.Json.JsonElement`
on the envelope record so the strategy can deserialize into its own typed DTO
(`PeriodReportData`, `IsoWeekReportData`, …) lazily.

Per-`ReportType` payload shapes:

- `Period`: `data = { start: <calendar-date>, end: <calendar-date> }`. Both
  bounds are **inclusive** on the date axis; the strategy interprets the
  range as `[start 00:00:00, end 23:59:59]` against each transaction's
  `Timestamp`.
- `IsoWeek`: `data = { week: <string in ISO 8601 form, e.g. "2026-W19"> }`.
  The strategy parses the string into the Monday–Sunday calendar-date range
  and applies the same `[from 00:00:00, to 23:59:59]` rule.

`ReportResult` shape (NON-NEGOTIABLE):

- `type` — the `ReportType` enum value, echoed from the request.
- `period` — a string descriptor of the resolved coverage. `"yyyy-Www"` for
  `IsoWeek`; `"yyyy-MM-dd..yyyy-MM-dd"` for `Period`. Future report types
  document their own descriptor format.
- `incomeTotal` — positive-or-zero sum of `Income`-direction transaction
  amounts in the period.
- `expenseTotal` — positive-or-zero sum of `Expense`-direction transaction
  amounts in the period.
- `netTotal = incomeTotal − expenseTotal` (may be negative).
- `categoryBreakdown` — a read-only collection of `CategoryBreakdownItem`,
  each of which carries exactly `{ category: <name>, total: <signed decimal> }`.
  The sign on `total` IS the direction indicator (positive = income-side,
  negative = expense-side); the item has no separate direction field and no
  transaction count.

`ReportResult` MUST NOT carry a list of contributing transactions, a
`currency` field, or any other report metadata beyond the six fields above.
Consumers needing the underlying transactions query the transactions endpoint
with a date filter separately.

Aggregation invariants (enforced by tests):

- Amounts on `TransactionResponse` are always **positive**; direction is
  carried by `transactionType`. Amounts inside `categoryBreakdown` are
  **signed**.
- `categoryBreakdown` is sorted income-side first (positive `total`), then
  expense-side (negative `total`); within each of those groups, by category
  name ascending.
- **Multi-category attribution**: a transaction tagged with N categories
  contributes its full signed amount to each of those N breakdown items.
  Transactions are NOT split or pro-rated across their categories. As a
  documented consequence, the arithmetic sum of `categoryBreakdown[*].total`
  MAY exceed `netTotal` in absolute value when multi-tagged transactions
  exist — this is by design, not a defect.
- Reports are **ad-hoc** and MUST NOT be persisted, cached, or otherwise
  stored. Two calls with identical input and unchanged entity state produce
  byte-identical responses.
- HTTP error mapping: missing or invalid `data` fields MUST produce HTTP
  400; an unknown `ReportType` enum value MUST produce HTTP 400; a `Period`
  payload with `start > end` MUST produce HTTP 400; a malformed
  `IsoWeek.week` string MUST produce HTTP 400.

For the MVP, only `PeriodReportStrategy` is in scope. `IsoWeekReportStrategy`
and `MonthReportStrategy` MUST NOT be scaffolded until explicitly requested.

**Rationale**: This pattern is the project's reusable AI scaffolding target
(`report-strategy-scaffold`). Keeping the parse-validate-aggregate-return
shape clean and copyable is what allows new report types to be generated by
an AI assistant with predictable structure and predictable tests. The typed
envelope (`ReportType` enum + per-type `data` DTO) makes unknown report types
fail at request binding rather than deep in the factory, and the summary-only
`ReportResult` keeps responses small and forces consumers to query
transactions through the dedicated endpoint when they want them.

### III. Seeded In-Memory Determinism

The Data layer MUST be the only source of truth for entities, MUST run entirely
in-memory, and MUST be reset on every application restart. Persistent storage,
EF Core, SQL Server, LocalDB, and database migrations are out of scope.

Concrete obligations:

- Repositories MUST be registered as DI **singletons** so data persists across
  requests for the lifetime of the process.
- Identifiers on both `Transaction` and `Category` are `int`. Seed data MUST
  use **deterministic integer IDs** (e.g., 1, 2, 3, …) stable across runs so
  tests can assert on specific identifiers. `Guid` MUST NOT be used for entity
  identity anywhere in the codebase.
- `Transaction.Timestamp` is a full `DateTime` (year–second precision in the
  user's local clock) — NOT `DateOnly`, NOT `DateTimeOffset`. The single-user
  MVP has no timezone concept.
- `Transaction.CategoryIds` is a non-empty `IReadOnlyList<int>` — a single
  transaction MAY be tagged with one or more categories. Every attached
  category's `CategoryType` MUST be compatible with the transaction's
  `TransactionType`: an `Expense` transaction MAY carry any combination of
  `Expense`/`Both` categories but no `Income`-only category, and symmetrically
  for `Income`. Validation enforcement of this rule is a Business-layer
  concern; the entity contract permits any non-empty collection of integer
  category references.
- Duplicate category names MUST be rejected **case-insensitively**.
- Transaction `Amount` in requests MUST be `> 0`; direction is encoded by
  `TransactionType` (`Income` or `Expense`).
- All amounts are USD. There MUST NOT be a `currency` field on any entity,
  DTO, or response — multi-currency support is explicitly out of scope.

**Rationale**: Determinism is what makes the in-memory MVP testable and what
makes MCP-style context replay meaningful. Non-deterministic seed IDs or
request-scoped repositories silently break both the test suite and the replay
story. Integer IDs (replacing the earlier GUID convention) keep URLs and test
assertions readable without sacrificing the determinism property. The
multi-category rule reflects the real-world observation that a single
transaction can legitimately belong to several categories at once (e.g.,
Groceries + Health Food) and that any "primary + tags" hack would compromise
the per-category breakdown numbers in reports.

### IV. Test-First with xUnit v3

Tests MUST live in dedicated test projects under
`src/backend/FinanceTracker/tests/`, organised as one unit-test project per
production project (`Finance.Data.UnitTests`, `Finance.Business.UnitTests`,
`Finance.Api.UnitTests`) plus a dedicated `Finance.Api.IntegrationTests`
project for end-to-end API coverage. All four MUST use **xUnit v3**
(`xunit.v3`, `OutputType=Exe`) as the runner and the **built-in
`Xunit.Assert` API** for assertions. **FluentAssertions is NOT used in this
project** — no third-party assertion library is referenced. Tests SHOULD be
written before or alongside the production code they exercise; a feature is
not "done" until its test coverage compiles, runs, and passes locally and in
CI.

Required coverage areas (the test suite MUST contain at least one test per
bullet):

- Domain entity construction, immutability, and structural equality for
  `Transaction` and `Category` (in `Finance.Data.UnitTests`).
- Multi-category transactions: order preservation in `Transaction.CategoryIds`
  and read-only collection exposure (in `Finance.Data.UnitTests`).
- DTO ↔ JSON round-trip fidelity for every DTO that crosses the HTTP boundary,
  including `decimal` precision, second-level `DateTime` precision,
  `ReportType` enum-name preservation, and ordered collections (in
  `Finance.Business.UnitTests`).
- Entity ↔ DTO mapper correctness for `TransactionMapper` and `CategoryMapper`
  (in `Finance.Business.UnitTests`).
- Category creation and validation, including case-insensitive duplicate
  rejection (in `Finance.Business.UnitTests` or `Finance.Data.UnitTests`
  depending on the layer that enforces the rule).
- Transaction creation and validation, including the `Amount > 0` rule and
  the multi-category compatibility rule from Principle III (in
  `Finance.Business.UnitTests`).
- Repository behaviour (singleton lifetime, seeded data shape, integer-ID
  determinism) — in `Finance.Data.UnitTests` when that feature ships.
- Report strategy selection by `ReportType` enum value (unknown value → 400),
  in `Finance.Business.UnitTests`.
- Period report aggregation and the boundary edge cases / multi-category
  attribution invariants in Principle II — in `Finance.Business.UnitTests`.
- Category breakdown logic, including the income-first / expense-second /
  alphabetical-within-group ordering and signed `total` semantics — in
  `Finance.Business.UnitTests`.
- Export logic for JSON and CSV — split across `Finance.Business.UnitTests`
  (format generation) and `Finance.Api.IntegrationTests` (content
  negotiation) when that feature ships.
- API endpoint integration coverage (status codes, request binding,
  serialization) — in `Finance.Api.IntegrationTests` when controllers ship.
- MCP context serialization, replay consistency, and sensitive-field
  redaction — in `Finance.Business.UnitTests` when that milestone lands.

CI MUST run `dotnet restore` → `dotnet build --no-restore` →
`dotnet test --no-build` with no external dependencies (no Docker, no
SQL Server, no LocalDB).

**Rationale**: Without a database to lean on, tests are the only mechanism
that keeps invariants like "amounts are always positive", "period bounds are
inclusive", or "multi-category transactions contribute to every attached
category's breakdown line" honest. Assertion style is unified by the spec's
reference test list (in each feature's `ai-artifact` spec or its `specs/NNN-*`
quickstart.md), not by a particular assertion library; the project uses plain
`Xunit.Assert` to keep the dependency surface minimal and to avoid the
FluentAssertions v7 → v8 commercial-license cliff. Older `ai-artifacts/`
specs still phrase their required-test lists in FluentAssertions style; those
phrasings translate one-to-one to `Xunit.Assert` equivalents (`Assert.Equal`,
`Assert.True`, `Assert.Throws<T>`, `Assert.Collection`,
`Assert.IsAssignableFrom<T>`, etc.) when the corresponding feature is
implemented.

### V. AI-Assisted Development Transparency

This is an AI-assisted-development pet project, and the AI workflow is itself a
graded deliverable. Every **meaningful** AI interaction (accepted or rejected)
MUST be appended to `ai-artifacts/agent_log.txt` with: timestamp, model / tool,
prompt, AI suggestion, decision (accepted / rejected), and reason. "Meaningful"
means anything that produced or rejected non-trivial code or design changes;
trivial completions (single-token autocompletes, formatting) are exempt.

Additional obligations:

- Transaction descriptions may contain sensitive text. The MCP context layer
  (when built) MUST support pruning and redaction before any context leaves
  the process. The project MUST NOT store or expose bank account numbers,
  card numbers, real personal identifiers, API keys, passwords, access
  tokens, or production secrets — even in test fixtures.
- The OpenAPI document (`/openapi/v1.json`) and Scalar reference UI
  (`/scalar/v1`) MUST be gated behind `app.Environment.IsDevelopment()` in
  `Program.cs` and MUST NOT be exposed in Release builds.

**Rationale**: The point of the project is not to accept all AI suggestions but
to evaluate them critically. The log is the artifact that proves that
evaluation happened. Redaction and dev-only API docs are the privacy/security
floor below which the MVP MUST NOT drop.

## Technology & Scope Constraints

The following stack and scope decisions are binding for the MVP. Any deviation
MUST go through the amendment process in Governance.

**Required stack**:

- Runtime / framework: **.NET 10** (`net10.0`) on ASP.NET Core Web API, C#.
  All projects (`Finance.Api`, `Finance.Business`, `Finance.Data`, and the
  four test projects under `src/backend/FinanceTracker/tests/`) MUST target
  `net10.0`. Downgrading to net8/net9 is NOT permitted without an amendment.
- API documentation: `Microsoft.AspNetCore.OpenApi` +
  `Scalar.AspNetCore`. Swashbuckle MUST NOT be reintroduced.
- Testing: **xUnit v3** with the built-in `Xunit.Assert` API
  (see Principle IV). FluentAssertions is NOT used.
- CI: GitHub Actions, at `.github/workflows/ci.yml`, with no external
  dependencies.

**Out of scope** for the MVP — these MUST NOT be added without an amendment:

- SQL Server, EF Core, LocalDB, database migrations, or any persistent store.
- Docker / container orchestration.
- Authentication, authorization, multi-user support.
- Frontend UI of any kind.
- Bank or payment integrations.
- Real financial advice features.

**Architectural defaults** (project layout, reproduced here so plans can gate on
it without re-reading the README):

- Solution file at `src/backend/FinanceTracker/FinanceTracker.slnx`.
- Three production projects under `src/backend/FinanceTracker/`
  (`Finance.Api`, `Finance.Business`, `Finance.Data`) plus four test projects
  under `src/backend/FinanceTracker/tests/` (`Finance.Data.UnitTests`,
  `Finance.Business.UnitTests`, `Finance.Api.UnitTests`,
  `Finance.Api.IntegrationTests`).
- AI artifacts under `ai-artifacts/`; specifications under
  `ai-artifacts/Specifications/`.

## Development Workflow & Quality Gates

**Constitution Check** (run by `/speckit-plan` before Phase 0 and after Phase
1): every plan MUST verify, at minimum, that

1. Dependency direction is preserved (Principle I).
2. Any new report type uses the factory + strategy pattern (Principle II) and
   no aggregation leaks into controllers.
3. New data lives in singleton in-memory repositories with deterministic seed
   IDs (Principle III).
4. Required test coverage areas (Principle IV) are addressed in `tasks.md`.
5. No out-of-scope technology from the section above is introduced.

Any deviation MUST be captured in the plan's **Complexity Tracking** table with
an explicit "Simpler Alternative Rejected Because" justification.

**Branching and commits**: feature work happens on feature branches named per
the speckit convention (numeric prefix + kebab-case slug). Specify Kit `git`
hooks auto-offer commits after constitution, specify, clarify, plan, tasks,
implement, checklist, analyze, and taskstoissues steps; accepting these
commits is the default. Force pushes to `main` are prohibited.

**Review expectations**: PR reviewers (human or AI) MUST verify the five
Constitution Check items above before approving. A PR that violates a
NON-NEGOTIABLE principle MUST be rejected, not patched, until the violation
is removed or an amendment is filed.

**CI gate**: a PR MUST NOT merge unless `dotnet restore`, `dotnet build
--no-restore`, and `dotnet test --no-build` all pass on the GitHub Actions
runner.

## Governance

This constitution supersedes any other documented practice in the repository.
Where the README, `CLAUDE.md`, or a spec under `ai-artifacts/Specifications/`
conflicts with this document, the constitution wins, and the conflicting
document MUST be updated to align in the same change set.

**Amendment procedure**:

1. Propose the change in the PR description, identifying the principle or
   section affected and the version bump category (MAJOR / MINOR / PATCH).
2. Run `/speckit-constitution` to update `.specify/memory/constitution.md`
   and propagate consistency changes to dependent templates and docs.
3. Update the **Sync Impact Report** comment at the top of this file.
4. Update `Last Amended` to the merge date and `Version` per the rules below.

**Versioning policy** (semantic versioning of the governance document, not the
software):

- **MAJOR**: a principle is removed, redefined in a backwards-incompatible
  way, or the dependency direction / scope envelope changes.
- **MINOR**: a new principle or governance section is added, or guidance is
  materially expanded.
- **PATCH**: wording clarifications, typo fixes, non-semantic refinements.

**Compliance review**: at the end of each meaningful feature milestone
(see README "Project Status"), the implementer MUST do a quick audit that
running code still honours each principle, and MUST file an amendment if
reality has drifted from the constitution. Drift is a defect, not a
documentation issue.

**Runtime guidance**: day-to-day implementation guidance for AI assistants
lives in `CLAUDE.md`. Where `CLAUDE.md` adds operational detail beyond this
constitution, that detail is authoritative for behaviour; where the two
disagree on a principle, this constitution governs.

**Version**: 2.0.1 | **Ratified**: 2026-05-18 | **Last Amended**: 2026-05-21
