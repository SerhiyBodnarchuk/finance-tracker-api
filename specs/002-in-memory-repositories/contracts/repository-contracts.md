# Contracts: Repository Public Surface

**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md) | **Data Model**: [data-model.md](../data-model.md)

This feature exposes **no HTTP endpoints** and **no DTOs**. The "contract" it owns — the surface that downstream features (controllers in feature 003+, the period-report strategy later) will consume — is the two C# **repository interfaces** declared in `Finance.Data.Repositories`. This file documents them as the contractual artifact, in the same role that JSON Schemas played for feature 001.

The interface signatures themselves are duplicated here verbatim from [data-model.md §1](../data-model.md#1-repository-interfaces); this file adds the **behavioral contract** (preconditions, postconditions, return-shape guarantees, exception conditions) for each member, which is what downstream features depend on and what a contract test would assert.

## `ITransactionRepository`

```csharp
namespace Finance.Data.Repositories;

public interface ITransactionRepository
{
    IReadOnlyCollection<Transaction> GetAll();
    Transaction? GetById(int id);
    Transaction Add(Transaction transaction);
    bool Delete(int id);
}
```

### `GetAll()` → `IReadOnlyCollection<Transaction>`

- **Postcondition**: returns every transaction currently in the store, in insertion order (seeded records, then any successful runtime `Add` calls in the order they were accepted).
- **Returned-collection guarantee**: the returned reference is read-only. Implementations MUST return either `_list.AsReadOnly()`, `_list.ToArray()`, or another type that prevents the caller from mutating the underlying store. The caller MUST NOT attempt to cast or downcast to a mutable collection type — doing so is a contract violation, and a future implementation may legitimately switch to an immutable backing structure that would make such a cast fail at runtime.
- **Empty case**: if the store is somehow empty (cannot happen with seed loaded, but conceivable in a future "reset" feature), returns an empty collection — never `null`.
- **Idempotency**: two adjacent `GetAll()` calls with no intervening write return collections with the same contents in the same order.
- **Cost**: O(n) in the number of stored transactions (the implementation may pay an O(1) wrapper cost for `AsReadOnly()`; that's allowed).

### `GetById(int id)` → `Transaction?`

- **Precondition**: none. Any `int` value (including negative, zero, and values that have never been issued) is a valid input.
- **Postcondition (hit)**: returns the exact stored `Transaction` whose `Id` equals the requested `id`. Because `Transaction` is an immutable record, this can be the same reference held in the store — no defensive copy is required.
- **Postcondition (miss)**: returns `null`. Never throws.
- **Idempotency**: pure.

### `Add(Transaction transaction)` → `Transaction`

- **Precondition**: `transaction` is not null. The `Id` field of the input is **ignored** — the repository overwrites it with the next id from its internal counter. Every other field is stored verbatim (in particular: the repository does NOT inspect `Amount`, `Type`, or `CategoryIds` for validity; cross-entity referential checks and `TransactionType`/`CategoryType` compatibility are Business-layer concerns).
- **Postcondition**: the returned `Transaction` is the stored form with the assigned id. Its `Id` is strictly greater than every previously issued id within this process (across both seeded records and prior runtime adds). A subsequent `GetById(returned.Id)` returns the same record; a subsequent `GetAll()` includes it at the end of the insertion-ordered sequence.
- **Side effects**: the underlying store gains exactly one record. The internal id counter advances by exactly one. (For the transaction repository there is no rejection path; the "no id holes" rule from spec FR-007 / SC-005 applies trivially.)
- **Exceptions**: none under the documented contract. `ArgumentNullException` if `transaction` is `null` is acceptable — the parameter is a non-nullable reference type per the nullable-reference-types feature, so a `null` here is a caller bug, not a domain rejection.

### `Delete(int id)` → `bool`

- **Precondition**: none.
- **Postcondition (hit)**: the record whose `Id` equals `id` is removed from the store. The method returns `true`. The id of the removed record is **not** recycled — the next `Add` issues a new id from the counter, not the freed one.
- **Postcondition (miss)**: no mutation, returns `false`.
- **Exceptions**: never throws under the documented contract.

## `ICategoryRepository`

```csharp
namespace Finance.Data.Repositories;

public interface ICategoryRepository
{
    IReadOnlyCollection<Category> GetAll();
    Category? GetById(int id);
    Category Add(Category category);
}
```

### `GetAll()` → `IReadOnlyCollection<Category>`

Same shape and guarantees as `ITransactionRepository.GetAll()`. The seed contains five categories with ids 1–5.

### `GetById(int id)` → `Category?`

Same shape and guarantees as `ITransactionRepository.GetById`. Returns `null` on miss; never throws.

### `Add(Category category)` → `Category`

- **Precondition**: `category` is not null. The `Id` field of the input is ignored. `Name` is treated as a case-insensitive identity key.
- **Success postcondition**: same as the transaction repository — stored form returned, id is strictly greater than every previously issued category id, `_nextId` advances by one.
- **Rejection postcondition** (duplicate name): the method **throws `InvalidOperationException`** with a message of the form `"A category named '{name}' already exists."`. The store is NOT mutated. The internal id counter does NOT advance — the next successful `Add` consumes the same id that was about to be used (spec FR-007 / SC-005).
- **Comparison semantics**: `Name` equality for the duplicate check uses `StringComparison.OrdinalIgnoreCase`. Surrounding whitespace is **not** trimmed; `" Salary"` is a different name from `"Salary"` for purposes of this check. (Future feature may decide to trim at the validation layer; this contract is the storage-level identity rule only.)
- **No `Delete`**: by user direction, the category repository is append-only in this feature. A future feature can add `Delete` if a real driver emerges; doing so would be a contract addition, not a contract change.

## Lifetime contract

Both interfaces are bound to **singleton** lifetimes in `Finance.Api/Program.cs`. Consumers in later features (controllers, business services) MAY safely cache the resolved instance — repeated resolution from the same `IServiceProvider` returns the same object. State writes are visible to every subsequent read from the same process. Process restart resets the store to the seeded baseline (spec FR-009).

## Backward / forward compatibility

- **Adding a method** to either interface is a breaking change in C#. If `Delete` for categories or `Update` for either entity is ever added, that addition belongs to its own feature and to its own spec; this feature's contract is intentionally minimal.
- **Changing a return type** (e.g., switching `IReadOnlyCollection<T>` to `IReadOnlyList<T>`) is also a breaking change at the source level even though it would be source-compatible from a callsite. Avoid.
- **Changing semantics without changing signatures** (e.g., starting to validate `Amount > 0` inside `Transaction.Add`, or starting to throw on `Delete` miss) silently breaks callers that were relying on the documented contract. Such a change requires a new feature and a callsite audit. The constitution Principle I split (Data stores, Business validates) is the project's main defense against this drift.

## Contract-test mapping

| Contract clause | Test that verifies it |
|---|---|
| `GetAll` returns insertion order | `GetAll_returns_five_seeded_transactions_in_id_order` / `…_seeded_categories_in_id_order` |
| `GetAll` is read-only / non-null | Same tests assert `Assert.Equal(5, all.Count)` and (where practical) attempt-to-mutate guards |
| `GetById` hit | `GetById_returns_seeded_transaction_by_literal_id` / `…_seeded_category_by_literal_id` |
| `GetById` miss → `null` | `GetById_returns_null_for_unknown_id` (both repos) |
| `Add` assigns id and persists | `Add_appends_new_transaction_and_assigns_id_six`, `Add_appends_new_category_and_assigns_next_id` |
| `Add` ignores input id | Covered inside `Add_appends_…` (input `Id = 0`, returned `Id == 6`) |
| `Add` rejects duplicate name + no id hole | `Add_rejects_duplicate_name_case_insensitively_without_consuming_id` |
| `Delete` hit | `Delete_existing_returns_true_and_removes_record` |
| `Delete` miss → `false` (no throw) | `Delete_missing_returns_false_and_does_not_throw` |
| Singleton lifetime | `Resolving_repository_twice_from_same_provider_yields_same_instance` |
| Perf bar (SC-004) | `Adds_one_hundred_transactions_with_reads_under_fifty_milliseconds` |

The full test list lives in [data-model.md §5](../data-model.md#5-tests).
