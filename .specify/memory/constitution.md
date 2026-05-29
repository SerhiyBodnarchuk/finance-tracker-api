<!-- Amendment history: .specify/memory/constitution-history.md (append-only; never read during normal work) -->

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
  concerns, validators, services, or DTOs.
- `Finance.Business` owns **all DTOs** — both the HTTP request/response
  shapes (`TransactionCreateRequest`, `TransactionResponse`,
  `CategoryCreateRequest`, `CategoryResponse`) and the report contracts
  (`ReportRequest`, `PeriodReportData`, `IsoWeekReportData`, `ReportResult`,
  `CategoryBreakdownItem`) — plus the `ReportType` enum, the trivial 1:1
  mappers between domain entities and DTOs (`TransactionMapper`,
  `CategoryMapper`), the centralized `JsonSerializationOptions`, the
  **application service layer** under `Services/` (`ICategoryService`,
  `ITransactionService`, `IReportService` and their implementations — the
  layer controllers route through), the **report factory and strategies**
  under `Services/Reports/` (the `ReportStrategyFactory` plus every
  `IReportStrategy` implementation, plus `ReportValidationException`),
  the **validation result data shapes** under `Validation/` (`ValidationResult`,
  `ValidationError`) consumed by validators in the API layer and by
  `ReportValidationException` in the Business layer, aggregation logic, and
  (later) MCP abstractions. It MUST NOT reference ASP.NET Core types or HTTP
  primitives.
- `Finance.Api` owns controllers / minimal-API endpoints, model binding
  (reusing the Business-layer DTOs directly), status-code mapping, OpenAPI +
  Scalar wiring, the **`Infrastructure/` folder** (the `ProblemDetailsMappers`
  helper and the **validators under `Infrastructure/Validators/`** —
  `ITransactionValidator` / `TransactionValidator` /
  `ICategoryValidator` / `CategoryValidator`), and DI composition.
  API-specific models MUST NOT be introduced here unless there is a concrete
  need beyond the Business DTOs (none for the MVP).

Cross-cutting access rules (NON-NEGOTIABLE):

- Controllers MUST go through Business-layer services
  (`ITransactionService`, `ICategoryService`, `IReportService`) for all data
  operations. Controllers MUST NOT reference repositories directly. The
  controller-to-service-to-repository hop is the spine that keeps
  aggregation, mapping, and storage concerns separate.
- Validators in `Finance.Api/Infrastructure/Validators/` MUST depend on
  Business-layer services for any cross-entity data they need (existence
  checks, type-compatibility lookups). Validators MUST NOT reference
  repositories directly — the same access rule that applies to controllers.
- **The API layer never sees a domain entity.** Services return DTOs;
  controllers and validators consume DTOs. The Business-layer mappers do
  the entity-to-DTO conversion at the service boundary, before any data
  crosses the layer line.

**Rationale**: This separation is the spine of the project and the reason
the MVP can stay testable without a database. Any aggregation logic in the
API layer, or any repository access from a controller or validator, breaks
both the MCP replay story and the test pyramid. Centralising DTOs and
services in the Business layer (rather than duplicating either in API)
keeps "what the application accepts and emits" defined in one place and
lets the API stay a thin transport adapter. Placing validators in the API
layer (a v3.0.0 redefinition of the v2.0.1 wording that put them in Business)
reflects that validators are HTTP-shaped concerns — they translate request
DTOs into `ValidationProblemDetails`, which only makes sense at the API
boundary. The *rules* they enforce still come from Business (compatibility
tables, existence checks via services), but the executors live where their
output is consumed. The explicit "API never sees a domain entity" rule is
what makes that boundary testable in isolation.

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
(`xunit.v3`) as the test framework, executed through the standard
**`Microsoft.NET.Test.Sdk`** (17.x) + **`xunit.runner.visualstudio`** (3.x,
the v3-compatible VSTest adapter) pair, and the **built-in `Xunit.Assert`
API** for assertions. **FluentAssertions is NOT used in this project** —
no third-party assertion library is referenced. Mocking in unit tests uses
**Moq** (`Moq` 4.20.x) where the system under test needs to be isolated
from its collaborators; integration tests against the API use
**`Microsoft.AspNetCore.Mvc.Testing`**'s `WebApplicationFactory<Program>`.
All four are test-only dependencies and MUST NOT be referenced from
production code. Tests SHOULD be written before or alongside the production code they
exercise; a feature is not "done" until its test coverage compiles, runs,
and passes locally and in CI.

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
- Application-service behaviour (`CategoryService`, `TransactionService`,
  `ReportService`): mapping, delegation to repositories / strategies, and
  exception propagation for cross-cutting policies like the duplicate-name
  conflict. Repositories MUST be mocked (Moq) so each test exercises exactly
  the service. (In `Finance.Business.UnitTests`.)
- Controller-level HTTP shape (status code mapping, `Location` header on
  201 Created, `ProblemDetails` / `ValidationProblemDetails` composition on
  400/404/409, short-circuit when validation fails). Services and validators
  MUST be mocked (Moq). (In `Finance.Api.UnitTests`.)
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

**`agent_log.txt` is append-only and MUST NOT be read.** Entries MUST NOT be
deleted, truncated, or rewritten. The only permitted write operation is appending
a new block at the end of the file. Any tool or skill that updates this log MUST
use an append-only write, never a read or overwrite.

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
  (see Principle IV). FluentAssertions is NOT used. Execution via
  **`Microsoft.NET.Test.Sdk`** (17.x) + **`xunit.runner.visualstudio`**
  (3.x). Mocking via **Moq** (`Moq` 4.20.x) for unit tests; integration
  tests use **`Microsoft.AspNetCore.Mvc.Testing`**'s
  `WebApplicationFactory<Program>`. All four are test-only dependencies
  and MUST NOT be referenced from production code.
- CI: **GitHub Actions** at `.github/workflows/ci.yml` (shipped by
  feature 005-ci-pipeline), with no external dependencies. Runs on
  `ubuntu-latest`, restores → builds in `Release` → tests via the
  VSTest path on every pull request to `main` or `development`.

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

**Version**: 3.0.2 | **Ratified**: 2026-05-18 | **Last Amended**: 2026-05-28
