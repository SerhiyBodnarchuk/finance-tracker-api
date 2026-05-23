# Phase 0 Research: Transactions / Categories Endpoints with Business Validation and Period Reports

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Design decisions for this feature, recorded before any code is written. Each section names the open question, the option chosen, the reasoning, and the alternatives considered.

## 1. Validation result shape — return-based or exception-based

**Open question**: Should validators report failure by returning a `ValidationResult { IsValid, Errors }` value object (controllers branch on it), or by throwing a `ValidationException` (an action filter / middleware catches it and writes the `400`)?

**Decision**: **Return-based.** Validators are pure functions that return a `ValidationResult` record with `IsValid : bool` and `Errors : IReadOnlyList<ValidationError>`, where `ValidationError` is `{ Field, Message }`. Controllers consume `ValidationResult` directly and translate to `BadRequest(problemDetails)` when invalid.

**Rationale**:

- Validation failures are normal-flow, expected outcomes — not exceptional. Throwing for a 400 makes the happy path the exception path, which inverts the semantics of `throw`.
- Return-based validation has no implicit dependency on a global filter / middleware to translate the result, which makes the unit-test setup trivial (call validator, assert on the returned record — no `WebApplicationFactory` needed for this layer).
- ASP.NET Core already has `ProblemDetailsFactory` and `ValidationProblemDetails` built in; the controller composes a `ProblemDetails` body from the `ValidationResult` and returns `BadRequest(problemDetails)`. No custom middleware is added in this feature.
- Errors are richer than a single message: each `ValidationError` names the failing field, which lets the problem-details body have a per-field error map (RFC 7807-style).

**Alternatives considered**:

- *Throw `ValidationException` + global exception filter*: Adds a global hook, tangles cross-cutting concerns, and obscures the per-call control flow in controllers. Rejected.
- *Return `bool` + out `errors` parameter*: C# 14 supports nicer return shapes; the `out`-pattern is harder to compose and harder to unit-test. Rejected.
- *Return `Result<T, ValidationError[]>` (discriminated-union)*: The project has no such type yet. Adding one for this feature is "abstractions beyond what the task requires" per CLAUDE.md. The `ValidationResult` record covers the same ground without introducing a new pattern.

## 2. Where the type-compatibility check lives

**Open question**: Constitution Principle III calls the multi-category transaction-type ↔ category-type compatibility rule a "Business-layer concern". Should it live in `TransactionValidator`, or in a separate `TransactionCategoryCompatibilityChecker` service?

**Decision**: **Inside `TransactionValidator`.** A single private helper method `CheckCategoryCompatibility(TransactionType type, IReadOnlyList<Category> categories)` runs after the basic shape checks (`Amount > 0`, non-empty `CategoryIds`, every id resolves).

**Rationale**:

- The four shape checks for a transaction create — `Amount > 0`, non-empty `Description`, non-empty `CategoryIds`, every id resolves, and type-compat — are all "is this request valid?". Splitting them across two service types invents a layer the spec doesn't ask for.
- The compatibility check needs the resolved `Category` records anyway (it reads `category.Type`), so the validator already has the data in hand from the referential-integrity check. Putting it in a separate service would force a second `_categoryRepository.GetAll()` call or a parameter-pass dance.
- All shape errors aggregate into the same `ValidationResult.Errors` collection, so the controller's single `if (!result.IsValid) return BadRequest(ToProblemDetails(result));` line handles every failure mode uniformly.

**Alternatives considered**:

- *Separate `TransactionCategoryCompatibilityChecker`*: Premature split. If a future feature needs to check compatibility from somewhere other than transaction-create (none planned), it can be extracted then.
- *In the controller*: Violates constitution Principle I — controllers MUST NOT contain validation rules.
- *In `InMemoryTransactionRepository.Add`*: Violates constitution Principle III bullet "Validation enforcement of this rule is a Business-layer concern; the entity contract permits any non-empty collection of integer category references."

## 3. Factory lookup data structure and how IsoWeek-without-a-strategy is handled

**Open question**: How does the factory map `ReportType` to `IReportStrategy`, and what happens when `type = IsoWeek` arrives (the enum value exists but the strategy is intentionally not scaffolded yet)?

**Decision**: `ReportStrategyFactory` holds a `Dictionary<ReportType, IReportStrategy>` populated **at construction time** from the DI-resolved `IEnumerable<IReportStrategy>` (each concrete strategy advertises its `ReportType` via a `Type` property on `IReportStrategy`). The factory exposes `IReportStrategy? TryGet(ReportType type)`. For `Period` the dictionary has an entry; for `IsoWeek` it does not (because no `IsoWeekReportStrategy` is registered); for any other enum value, same. `TryGet` returns `null` for missing entries, and the controller maps `null` to HTTP 400 with a problem-details body explaining the unsupported type.

**Rationale**:

- Dictionary lookup is O(1), no allocation per call, no string parsing, no reflection. The constitution-mandated "unknown enum value → 400" behavior falls out naturally — the missing-key case is just a normal dictionary miss.
- Populating the dictionary from `IEnumerable<IReportStrategy>` keeps the strategy registration loose: a new strategy is added to DI and the factory picks it up without code change in the factory itself. This is the AI-skill-friendliness the constitution Principle II rationale calls out.
- The `IsoWeek`-in-enum-but-not-in-factory situation is contractually correct: the enum is part of the public DTO surface (a client can serialize `"IsoWeek"` and the server will accept the binding), but the strategy is not yet implemented, so the factory cleanly rejects it. The 400 message names the offending type and hints that it is "not yet supported".

**Alternatives considered**:

- *Switch statement inside the factory*: Couples the factory to every concrete strategy; adding a future strategy requires editing the factory. The dictionary-from-DI approach decouples them.
- *Throw `NotSupportedException` for unknown enum values*: Forces controllers to use `try/catch`, which conflicts with the "validation is normal flow" stance from §1.
- *Make `ReportStrategyFactory` validate the enum range itself*: It already does — the dictionary miss IS the validation. Wrapping it in extra logic would be redundant.

## 4. Where `start > end` and date-shape validation live (in the strategy, not in the controller)

**Open question**: `Period` report requests have several payload-level invariants — `start` and `end` both present, both ISO-`yyyy-MM-dd`, `start <= end`. Does the strategy check those, or does the controller short-circuit on a deserialization error before reaching the strategy?

**Decision**: **Inside `PeriodReportStrategy`**, as the first three steps of its parse → validate → aggregate → return pipeline. The strategy deserializes `request.Data` into `PeriodReportData` (catching `JsonException` and surfacing it as a validation failure), then checks `start <= end`, then runs the aggregation.

**Rationale**:

- Constitution Principle II's "each strategy MUST own its full parse → validate → aggregate → return pipeline" bullet is unambiguous. The controller MUST NOT inspect `data`.
- Deserialization failures (e.g., `start: "not-a-date"`) and semantic failures (e.g., `start: "2026-06-01", end: "2026-05-01"`) are both report-level validation problems — the strategy is the natural home for them.
- Returning the validation failure as a structured `ReportResult`-or-error value is awkward; instead, the strategy throws a `ReportValidationException` (with the same `{ Field, Message }` collection as `ValidationResult`), and the controller catches **exactly** that exception type and maps to HTTP 400. This is a narrow scope for exceptions (one type, one mapping target) and avoids polluting `IReportStrategy.Generate`'s signature with a discriminated-union return.

**Alternatives considered**:

- *Make `IReportStrategy.Generate` return `Result<ReportResult, ValidationError[]>`*: Same "introduce a `Result` type" anti-pattern as §1, but worse because the happy-path return shape is already a record. Rejected for consistency.
- *Have the controller pre-validate the `data` payload using a strategy-specific helper*: Forces the controller to know each strategy's payload shape. Defeats the strategy pattern.

## 5. Error envelope: `ProblemDetails` vs. custom JSON

**Decision**: ASP.NET Core's **built-in `ProblemDetails`** (RFC 7807). Validation problems use `ValidationProblemDetails` (also built-in), which has a `errors` dictionary keyed by field name. Duplicate-name 409 uses a plain `ProblemDetails` with `status: 409, title: "Duplicate category name", detail: "..."`. Not-found 404 same shape with the relevant message.

**Rationale**:

- `ProblemDetails` is the standard ASP.NET Core 7+ error format. Clients written against it already know the shape. Custom JSON would be a parallel convention nobody asked for.
- `ValidationProblemDetails`'s per-field error map is exactly what the validator's `ValidationResult.Errors` aggregates into. The controller converts one to the other in one helper method.
- No new package needed — both types are in `Microsoft.AspNetCore.Mvc` which `Finance.Api` already references.

**Alternatives considered**:

- *Custom error-envelope record*: More code, less interoperability, and would have to be hand-documented in the OpenAPI document. Rejected.
- *Returning plain strings*: Loses status-code-specific structure. Rejected.

## 6. DI lifetimes for the new services

**Decision**:

- `ITransactionValidator` → `InMemoryTransactionValidator` — registered as **singleton**. Its only dependency is `ICategoryRepository` (also singleton). The validator has no per-request mutable state.
- `ICategoryValidator` → `InMemoryCategoryValidator` — registered as **singleton**. No dependencies; pure function.
- `IReportStrategy` → `PeriodReportStrategy` — registered as **singleton** (one of potentially many strategies, all registered via `services.AddSingleton<IReportStrategy, PeriodReportStrategy>()`). Dependencies (`ITransactionRepository`, `ICategoryRepository`) are also singleton. No per-request state.
- `IReportStrategyFactory` → `ReportStrategyFactory` — registered as **singleton**. Constructor receives `IEnumerable<IReportStrategy>` and builds its dictionary once.

**Rationale**:

- The repositories the validators and strategies depend on are singletons (constitution Principle III). Mixing in scoped or transient services on top would either be wasteful (rebuild the same stateless object per request) or risky (capturing singleton dependencies in a scoped service that outlives the scope).
- Singletons match the spec's reusable-AI-skill goal — when a new strategy is added in a future feature, the registration pattern is the same one-liner each time. Discoverable, copyable, predictable.

**Alternatives considered**:

- *Scoped lifetimes*: Adds GC pressure per request for objects that have no per-request data to hold. Rejected.

## 7. Integration-test infrastructure choice

**Decision**: Use `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 with `WebApplicationFactory<Program>` to host the API in-memory inside each test class. Each `IClassFixture<ApiTestFixture>` test class shares one factory across its tests; **each test gets a fresh `HttpClient` instance and the factory state resets between test classes** by default (the factory itself is rebuilt per class). For tests that mutate state (POST then DELETE), the test class uses one factory and runs tests in defined order via xUnit v3's test-collection semantics — or, equivalently, each test creates its own factory if isolation is needed.

**Rationale**:

- `WebApplicationFactory` is the canonical ASP.NET Core integration-test entry point. It hosts the real Program.cs DI graph (real validators, real repositories with real seed data), so the tests exercise the same code paths the runtime will. The only thing it short-circuits is the Kestrel transport layer.
- The seed data is reset per `WebApplicationFactory` instance because each instance constructs a fresh `InMemoryTransactionRepository` / `InMemoryCategoryRepository` singleton. Tests that need pristine state can request a fresh factory.
- No new NuGet packages beyond `Microsoft.AspNetCore.Mvc.Testing` itself. No mocking framework — the tests run against real services, which is exactly what feature 002's repositories are designed for.

**Alternatives considered**:

- *Spin up Kestrel on a random port + HttpClient against `http://localhost:NNN`*: Heavier, slower, and platform-quirky on Windows. Rejected.
- *Unit-test the controllers in isolation with a mock validator*: Doesn't catch model-binding, content-negotiation, or routing problems. Loses the "API endpoint integration coverage" Principle IV bullet's intent. Used in addition would be redundant for the work this feature does.
- *Skip integration tests entirely and rely on Business-layer unit tests*: Loses the entire HTTP-shape verification, including the 201-with-Location header, the 404 mapping, and the 409 mapping. Rejected.

## 8. Removing the default `WeatherForecastController` scaffold

**Decision**: **Delete `Finance.Api/Controllers/WeatherForecastController.cs` and `Finance.Api/WeatherForecast.cs`** as part of this feature's PR. The OpenAPI document and Scalar UI no longer list `/WeatherForecast`.

**Rationale**:

- These files were the `dotnet new webapi` default and have never been part of the product surface. Leaving them would mean the OpenAPI document (now actively used by Scalar) advertises an endpoint that has nothing to do with the product.
- Cleanup that ships alongside the feature it replaces is the right time to do it. Waiting for a later "cleanup PR" risks the scaffold lingering and confusing readers.

**Alternatives considered**:

- *Leave them in place*: Pollutes the OpenAPI document and is the kind of dead code CLAUDE.md tells us to remove proactively. Rejected.

## 9. JSON / CSV export

**Decision**: **Out of scope.** Per the user's `/speckit-specify` direction, the export endpoints are not part of this feature. Spec Assumptions explicitly records this.

**Rationale**: The user said so. The feature is already substantial without it.

**Reopen-when**: a future feature is scoped specifically to export. That feature will likely add `ITransactionExportService` + `IExportFormatter` (per-format) in `Finance.Business/Exports/` and an `ExportController` in `Finance.Api`, plus content-negotiation tests in `Finance.Api.IntegrationTests`.

## Summary of decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Validation result shape | Return-based `ValidationResult` record with per-field `Errors`. No exceptions for input-shape failures. |
| 2 | Type-compatibility check location | Inside `TransactionValidator`. Helper method, same validation result. |
| 3 | Factory lookup + IsoWeek handling | `Dictionary<ReportType, IReportStrategy>` populated from DI. `TryGet` returns `null` for unregistered enum values; controller maps to 400. |
| 4 | Where `start > end` validates | Inside `PeriodReportStrategy`, as the first phase of its pipeline. Throws `ReportValidationException`; controller maps to 400. |
| 5 | Error envelope | Built-in `ProblemDetails` / `ValidationProblemDetails` (RFC 7807). No custom envelope. |
| 6 | DI lifetimes | All new services registered as singletons. |
| 7 | Integration-test infra | `Microsoft.AspNetCore.Mvc.Testing` + `WebApplicationFactory<Program>`. One new NuGet package, no mocking framework. |
| 8 | `WeatherForecastController` | Deleted as part of this feature. |
| 9 | JSON / CSV export | Out of scope per user direction. |

All nine decisions are encoded in [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md) as concrete C# signatures, HTTP contracts, and verification steps.
