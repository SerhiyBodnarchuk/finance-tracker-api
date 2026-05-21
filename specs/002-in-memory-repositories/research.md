# Phase 0 Research: Seeded In-Memory Repositories

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

This document records the design decisions taken before any code is written. Each section names the open question, the option chosen, the reasoning, and the alternatives considered. None of the questions in this feature required external research — the constraints come entirely from the spec, the constitution, and the existing feature-001 entities.

## 1. Time-of-day component of seeded transaction timestamps

**Open question**: `Transaction.Timestamp` is a full `DateTime` (year–second), but the spec lists seed transactions only by calendar date. What time of day does each seed transaction get?

**Decision**: Each seeded transaction uses **`new DateTime(2026, MM, DD, 12, 0, 0)` — noon local time on the stated date.**

**Rationale**:

- Spec Assumption #1 explicitly leaves the time-of-day to the implementer as long as every seed timestamp falls within its stated calendar day.
- Noon avoids two trap behaviors: it can't be confused with "midnight = start of next day" off-by-one, and it can't be confused with "23:59:59 = end of day" which is what the future period strategy uses internally for its upper bound. A noon timestamp is unambiguously "inside the day" by any reasonable interpretation.
- Picking the same time-of-day for every seed transaction also makes the seed easy to scan and test: assertions on a known timestamp are read as `new DateTime(2026, 5, 4, 12, 0, 0)` rather than five different per-row times.

**Alternatives considered**:

- *Midnight (`DateTime.MinValue`-style 00:00:00)*: Rejected because the future period strategy treats the lower bound as `[start 00:00:00, …]` inclusive — a midnight timestamp is fine but sits exactly on the bound, which is the most error-prone case to demo with. Noon is safer.
- *End-of-day (`23:59:59`)*: Same upper-bound concern, mirror-image. Rejected.
- *Five distinct per-row times (e.g., 09:15, 12:30, 18:45, …)*: Adds noise to tests and seed listings without buying anything; the report features don't sub-divide a calendar day.
- *Mark all seeds with `DateTime.UtcNow` of seed-load time*: Rejected outright — non-deterministic, violates constitution Principle III.

## 2. Identifier generation strategy

**Open question**: How are runtime ids assigned, and how do we guarantee they do not collide with the literal seed ids (categories 1–5, transactions 1–5) and do not leave gaps when an `Add` is rejected?

**Decision**: Each repository holds a private `int _nextId` field initialized to `6` (one past the highest literal seed id). `Add` builds the candidate stored record from the input using `_nextId` as the id, validates whatever it needs to validate (only `InMemoryCategoryRepository` validates anything — case-insensitive name uniqueness), and **only on the success path** appends to the list AND increments `_nextId`. The increment is the **last** statement in the success path.

**Rationale**:

- Spec FR-007 forbids id holes caused by rejected adds. The only way to honor that is to gate the increment on success — a "always increment then maybe roll back" approach is more error-prone and harder to read.
- Starting at `6` rather than `0` or `1` makes the seed ids `1..5` and the runtime ids `6, 7, 8, …` trivially distinguishable in tests and logs, while still being deterministic across runs (constitution Principle III).
- Using a plain `int` field is enough because the writes are single-process serial (see §4); no `Interlocked.Increment` is needed.

**Alternatives considered**:

- *`Interlocked.Increment(ref _nextId)` before validation, then roll back on rejection*: Adds a second source-of-truth (the field) that has to be kept in sync with the list — and rolling back is fiddly. Rejected.
- *`list.Max(t => t.Id) + 1` per `Add` call*: Linear-time per insert and re-scans an immutable property of items already in the list. Rejected as wasteful and fragile if a record's id is ever made nullable in the future.
- *`Guid.NewGuid()`*: Forbidden by constitution Principle III ("Guid MUST NOT be used for entity identity anywhere in the codebase").

## 3. Lookup data structure

**Open question**: Should the in-memory repositories back their reads with a flat `List<T>` and linear scan, or with a `Dictionary<int, T>` (or both) for O(1) `GetById`?

**Decision**: **`List<T>` only**, with linear scan for `GetById` (`list.FirstOrDefault(x => x.Id == id)`) and linear scan for `InMemoryCategoryRepository`'s duplicate-name check (`list.Any(c => string.Equals(c.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))`).

**Rationale**:

- Spec SC-004 sets the perf bar at 100 transactions / read under 50 ms. A linear scan over a few hundred records on a modern CPU is sub-microsecond; the `Dictionary` lookup would optimize a non-existent bottleneck.
- A single backing structure is simpler to reason about: one source of truth for "what's in the store", no risk of the list and the dictionary drifting if a future maintainer forgets to update both.
- The CLAUDE.md guidance ("Don't add features, refactor, or introduce abstractions beyond what the task requires") argues against the second structure.
- If a future feature (large-scale period reports, exports over 10k+ transactions) needs O(1) lookup, the swap-in is one `Dictionary<int, T>` field and three line changes — easy to do then.

**Alternatives considered**:

- *`Dictionary<int, T>` backing*: Loses insertion order, which `GetAll()` callers may quietly depend on (the future controllers feature returns the list as-is in HTTP responses). Insertion order would need a parallel `List<int>` of ids, which is exactly the "two sources of truth" problem this decision avoids.
- *`SortedDictionary<int, T>`*: Same drawback as `Dictionary`; gains a sort property nobody asked for.
- *`ConcurrentDictionary<int, T>`*: Solves a concurrency problem this feature explicitly says is out of scope (see §4). Rejected.

## 4. Concurrency stance

**Open question**: ASP.NET Core dispatches HTTP requests to multiple threads. The repositories are singletons. Do we need locking, `ConcurrentBag`, `ImmutableList<T>`, or similar?

**Decision**: **No locking, no concurrent collections.** Plain `List<T>` with no thread-safety guarantees. Spec Assumption #6 documents this explicitly: "Writes are accepted at single-process scope. There is no requirement to be safe under heavy concurrent traffic … no extra concurrency hardening beyond what the standard collection types already provide is needed for the MVP."

**Rationale**:

- The MVP is single-user demo software. The realistic worst case is one developer hitting the API from a browser tab and a curl one-liner roughly simultaneously — the chance of two `Add` calls landing inside `List<T>.Add` at the same millisecond is negligible and the failure mode (one record dropped, or a transient internal exception) is acceptable for an MVP.
- Introducing `lock`s or `ConcurrentDictionary<int, T>` now would be premature optimization, and the wrong abstraction if the future need turns out to be e.g. snapshot-isolation rather than mutual exclusion. Better to wait for a real driver.
- Documenting this stance in the spec **and** here makes the choice obvious to a future reviewer who notices the lack of locking.

**Alternatives considered**:

- *Wrap every public method in `lock (_sync)`*: Cheap, correct, but unjustified for the workload — see above.
- *Switch `List<T>` to `ImmutableList<T>` with `Interlocked.CompareExchange`*: Allocates a new list per write, which contradicts the lightweight, demo-grade design.
- *`ConcurrentBag<T>` / `ConcurrentDictionary<int, T>`*: Wrong shape — `ConcurrentBag` doesn't preserve insertion order, and `ConcurrentDictionary` reintroduces the two-sources-of-truth problem from §3.

**Reopen-when**: a feature lands that stress-tests the repositories under multi-threaded HTTP load (e.g., the future export feature streaming over thousands of transactions while concurrent edits happen). At that point the spec for that feature will revisit this assumption.

## 5. `GetById` return idiom (and `Delete` return idiom)

**Open question**: Spec FR-010 requires a "clearly distinguishable not-found signal". What's the C# idiom?

**Decision**:

- `GetById` returns `T?` (a nullable reference to the record). `null` ≡ not found.
- `Delete` returns `bool`. `true` ≡ found and removed; `false` ≡ no record with that id existed and the store was not mutated.

**Rationale**:

- Both signatures match the ai-artifacts spec's `Transaction? GetById(...)` and `bool Delete(...)` shapes verbatim, so the contract is the same one a reader of either spec would expect.
- The future controllers feature (003+) can map `null` → 404 and `false` → 404 with no further translation logic.
- Neither idiom throws — which spec FR-010 explicitly forbids ("not an exception and not a placeholder").
- C# records are reference types in `net10.0`, so `T?` is the nullable-reference-type form (the project has nullable enabled). No boxing concerns.

**Alternatives considered**:

- *Throw `KeyNotFoundException` on miss*: Forbidden by spec FR-010.
- *Return a `Result<T, Error>` discriminated-union type*: Premature; the project has no such type yet, and `T?` + `bool` together cover the four call sites a future controllers feature will have. Adding a `Result` type now is the "abstractions beyond what the task requires" trap from CLAUDE.md.
- *Add a separate `bool TryGetById(int, out T)` method*: The `T?` shape conveys the same information without a `Try*` prefix and without an `out` parameter. Either would work but the nullable shape is the more modern C# idiom and is what the ai-artifacts spec listed first.

## 6. Where validation lives

**Open question**: The ai-artifacts spec lists eight required tests, several of which are business-rule validations (`Amount > 0`, "transaction with unknown category fails", category↔type compatibility). Should the repository enforce these too, or only the case-insensitive duplicate-name uniqueness?

**Decision**: **Only the duplicate-name uniqueness lives in the repository.** Every other validation listed in the ai-artifacts spec is deferred to the business layer in a later feature.

**Rationale**:

- Spec Assumptions explicitly narrow scope: "Validation that lives above the storage boundary — for example, 'amount must be greater than zero', 'every referenced category exists', 'the chosen categories are compatible with the chosen transaction type' — is **out of scope for this feature** and is owned by the business layer in a later feature."
- The constitution's Principle III bullet on multi-category compatibility says verbatim: "Validation enforcement of this rule is a Business-layer concern; the entity contract permits any non-empty collection of integer category references." That is the same boundary applied to all the listed rules: the data layer stores; the business layer validates.
- Category-name uniqueness is the **one** invariant that is intrinsic to the category collection itself — it's about the shape of the data the store owns, not about cross-entity consistency or input bounds. Putting it in the repository (rather than a business service that has to be invoked everywhere a category is added) is the natural home.

**Alternatives considered**:

- *Repository enforces `Amount > 0` and "category exists"*: Would force the data layer to know about transaction types vs. category types (because the "category compatibility" rule cascades from there). Pulls business logic down into Data, violating Principle I.
- *Repository enforces nothing — even duplicate names are caller's responsibility*: Pushes a property of the category store itself out to every caller. Strictly worse than the chosen split; case-insensitive name uniqueness is the kind of invariant a future maintainer will assume the store already protects.

## 7. DI registration location

**Open question**: Should the `AddSingleton<ICategoryRepository, …>()` / `AddSingleton<ITransactionRepository, …>()` calls live directly in `Program.cs`, or behind a `Finance.Data.AddInMemoryRepositories(this IServiceCollection)` extension method?

**Decision**: **Two raw `AddSingleton<...>()` lines inside `Program.cs`.** No extension method.

**Rationale**:

- Two lines of DI registration are not a meaningful unit of reuse. The "introducing abstractions beyond what the task requires" anti-guidance in CLAUDE.md applies.
- The constitution is silent on the question — Principle I allows either: composition is the API layer's documented responsibility. Both shapes preserve dependency direction.
- An extension method becomes worth it once there are five-plus registrations or a deliberate "this is the whole storage layer being composed" intent. Neither holds yet.

**Alternatives considered**:

- *`Finance.Data.DependencyInjection.AddInMemoryRepositories()` extension*: The future controllers feature is going to add maybe one more registration (a date provider, perhaps) and the business layer will add validators. None of those are inside `Finance.Data`. If a `Data`-internal registration helper is ever wanted, it can be introduced at that point in a one-line refactor.

## Summary of decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Seed time-of-day | Noon local time on each seed date |
| 2 | Id generator | `int _nextId` starting at `6`, incremented only after a successful `Add` |
| 3 | Lookup structure | Plain `List<T>` with linear scan; no `Dictionary` |
| 4 | Concurrency | None — no locks, no concurrent collections, single-process serial assumption |
| 5 | Not-found idiom | `T? GetById(int)` returns `null`; `bool Delete(int)` returns `false` |
| 6 | Validation in repository | Only case-insensitive category-name uniqueness; everything else is Business-layer |
| 7 | DI registration | Two `AddSingleton<...>` lines in `Program.cs`; no extension method |

All seven decisions are recorded in [data-model.md](data-model.md) and [contracts/repository-contracts.md](contracts/repository-contracts.md) as concrete C# signatures and tests.
