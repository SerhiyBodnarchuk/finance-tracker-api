# Phase 1 — Data Model: Domain Entities and API DTOs

**Feature**: `001-domain-entities-dtos` · **Plan**: [plan.md](plan.md) · **Spec**: [spec.md](spec.md) · **Research**: [research.md](research.md)

Concrete C# signatures for every contract this feature introduces. The shapes here are the *normative* form — [contracts/](contracts/) holds the JSON-Schema view of the subset that crosses the HTTP boundary, and [quickstart.md](quickstart.md) shows how they compose.

All records target `net10.0` with nullable reference types enabled. All identifiers are `int`. All amounts are `decimal`. All timestamps are `DateTime` (naive, no offset). All calendar dates are `DateOnly`. All collection-typed properties are `IReadOnlyList<T>`. Every type below is `sealed` and declared with `public sealed record`.

---

## `Finance.Data` (project)

### Enums

```csharp
namespace Finance.Data.Models;

public enum TransactionType
{
    Income,
    Expense
}

public enum CategoryType
{
    Income,
    Expense,
    Both
}
```

Both enums serialize as their declared names (`"Income"`, `"Expense"`, `"Both"`) via the centralized `JsonStringEnumConverter` configured in `Finance.Business` (see [research.md §2](research.md#2-json-serialization-configuration-for-enums-dates-and-decimals)).

### `Transaction`

```csharp
namespace Finance.Data.Models;

public sealed record Transaction(
    int Id,
    DateTime Timestamp,
    string Description,
    decimal Amount,
    TransactionType Type,
    IReadOnlyList<int> CategoryIds
);
```

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | Unique, server-assigned. Seed data uses fixed integers (1, 2, …). |
| `Timestamp` | `DateTime` | Year–second precision in the user's local clock. No `DateTimeOffset`. |
| `Description` | `string` | Required, non-empty (enforcement is a downstream feature). |
| `Amount` | `decimal` | Unsigned (always > 0 in valid data). Direction is conveyed by `Type`. |
| `Type` | `TransactionType` | `Income` or `Expense`. |
| `CategoryIds` | `IReadOnlyList<int>` | Non-empty (enforcement downstream). Order is preserved. May contain duplicates at the contract level (deduplication is a downstream validation concern). |

Implements: spec FR-001, FR-002, FR-008 (immutable by construction).

### `Category`

```csharp
namespace Finance.Data.Models;

public sealed record Category(
    int Id,
    string Name,
    CategoryType Type
);
```

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | Unique, server-assigned. Seed data uses fixed integers. |
| `Name` | `string` | Required, non-empty. Case-insensitive equivalence used for duplicate detection downstream. |
| `Type` | `CategoryType` | `Income`, `Expense`, or `Both`. |

Implements: spec FR-003, FR-007 (case-insensitive name applies to *comparisons*, not storage — the entity stores the name as supplied), FR-008.

---

## `Finance.Business` (project)

### Enum

```csharp
namespace Finance.Business.Enums;

public enum ReportType
{
    Period,
    IsoWeek
    // Month, etc. — added when those report types are scheduled.
}
```

Implements: spec FR-006. Wire form: `"Period"`, `"IsoWeek"`.

> **MVP scope note (2026-05-21)**: only `Period` has a defined payload DTO in this feature. `IsoWeek` is retained in the enum as a *documented future value* so the README's canonical `ReportResult` example (a 2026-W20 ISO-week report) can serve as the US5 test fixture. The matching `IsoWeekReportData` DTO, its JSON schema, the `IsoWeekReportStrategy`, and the unknown-payload-shape model binding all ship together in a later feature. Submitting `{ "type": "IsoWeek", … }` to the future `POST /api/reports` will be rejected by the factory at runtime (no registered strategy) until that feature lands.

### DTOs — Transactions

```csharp
namespace Finance.Business.Dtos.Transactions;

public sealed record TransactionCreateRequest(
    DateTime Timestamp,
    string Description,
    decimal Amount,
    TransactionType Type,
    IReadOnlyList<int> CategoryIds
);

public sealed record TransactionResponse(
    int Id,
    DateTime Timestamp,
    string Description,
    decimal Amount,
    TransactionType Type,
    IReadOnlyList<CategorySummary> Categories
);

public sealed record CategorySummary(
    int Id,
    string Name
);
```

`TransactionCreateRequest` (FR-009) carries no `Id`. `TransactionResponse` (FR-015) carries an inline `Categories` collection in attachment order — `CategorySummary` is the per-item shape (`{ id, name }` in JSON). The README's [Period report example response](../../README.md#period-report-example) demonstrates the wire form.

### DTOs — Categories

```csharp
namespace Finance.Business.Dtos.Categories;

public sealed record CategoryCreateRequest(
    string Name,
    CategoryType Type
);

public sealed record CategoryResponse(
    int Id,
    string Name,
    CategoryType Type
);
```

`CategoryCreateRequest` (FR-010) carries no `Id`. `CategoryResponse` (FR-016) is a straight 1:1 mirror of `Category`.

### DTOs — Reports

```csharp
namespace Finance.Business.Dtos.Reports;

using System.Text.Json;
using Finance.Business.Enums;

public sealed record ReportRequest(
    ReportType Type,
    JsonElement Data
);

public sealed record PeriodReportData(
    DateOnly Start,
    DateOnly End
);

// IsoWeekReportData is intentionally not defined in this feature. It ships
// with the IsoWeekReportStrategy in a later feature; see the MVP scope note
// above the ReportType enum.

public sealed record CategoryBreakdownItem(
    string Category,
    decimal Total
);

public sealed record ReportResult(
    ReportType Type,
    string Period,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal NetTotal,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown
);
```

| DTO | Wire shape | Spec FR |
|---|---|---|
| `ReportRequest` | `{ "type": "Period", "data": { … } }` | FR-011 |
| `PeriodReportData` | `{ "start": "2026-05-01", "end": "2026-05-31" }` | FR-012 |
| `CategoryBreakdownItem` | `{ "category": "Groceries", "total": -180.25 }` | FR-019 |
| `ReportResult` | See README's [ISO-week report example](../../README.md#iso-week-report-example) — the wire shape is `ReportType`-agnostic; the example happens to use `Type = IsoWeek` as illustration | FR-018, FR-020, FR-021, FR-022 |

Notes:
- `ReportResult.Period` is a `string` descriptor — `"yyyy-Www"` for `IsoWeek`, `"yyyy-MM-dd..yyyy-MM-dd"` for `Period`. Future report types document their own descriptor format inside their strategy. This keeps the response shape uniform across `ReportType` values without a polymorphic-period field.
- `CategoryBreakdownItem.Total` is **signed** (positive = income side, negative = expense side). No `Direction` field, no `TransactionCount` field.
- `ReportResult.IncomeTotal` and `.ExpenseTotal` are always positive-or-zero; `.NetTotal = IncomeTotal − ExpenseTotal` and may be negative.
- The arithmetic sum of `CategoryBreakdown[*].Total` is **not** required to equal `NetTotal` when transactions in the period are multi-tagged — see [spec.md FR-020](spec.md#requirements-mandatory) for the documented attribution rule.

### Mappers

```csharp
namespace Finance.Business.Mappers;

using Finance.Business.Dtos.Categories;
using Finance.Business.Dtos.Transactions;
using Finance.Data.Models;

public static class CategoryMapper
{
    public static CategoryResponse ToResponse(this Category category) =>
        new(category.Id, category.Name, category.Type);

    public static Category ToEntity(this CategoryCreateRequest request, int assignedId) =>
        new(assignedId, request.Name, request.Type);
}

public static class TransactionMapper
{
    public static TransactionResponse ToResponse(
        this Transaction transaction,
        IReadOnlyDictionary<int, Category> categoriesById)
    {
        var categories = transaction.CategoryIds
            .Select(id => categoriesById.TryGetValue(id, out var category)
                ? new CategorySummary(category.Id, category.Name)
                : throw new KeyNotFoundException(
                    $"Transaction {transaction.Id} references unknown category {id}."))
            .ToList()
            .AsReadOnly();

        return new TransactionResponse(
            transaction.Id,
            transaction.Timestamp,
            transaction.Description,
            transaction.Amount,
            transaction.Type,
            categories);
    }

    public static Transaction ToEntity(this TransactionCreateRequest request, int assignedId) =>
        new(
            assignedId,
            request.Timestamp,
            request.Description,
            request.Amount,
            request.Type,
            request.CategoryIds);
}
```

Notes:
- Mappers are pure static functions; they take everything they need as plain arguments. No repository injection, no service dependency. This keeps them trivially testable.
- The category-lookup dictionary on the response path is provided by the caller (which, in the next feature, will be the service that orchestrates the controller + repository). Mapping a transaction that references an unknown category id throws `KeyNotFoundException` so the bug surfaces immediately at the boundary; the controller / strategy is responsible for ensuring the lookup is well-formed.
- The `ToEntity` direction takes the assigned id as a parameter because the request DTO does not carry it (per spec FR-009).
- `Transaction.CategoryIds` and `TransactionCreateRequest.CategoryIds` use the same `IReadOnlyList<int>` type — the `ToEntity` mapper reuses the supplied list reference rather than copying. This is safe because the request DTO is itself immutable. If a future requirement needs a defensive copy, it lives one line away.

---

## Relationships

```text
Transaction
  ├─ Id              : int
  ├─ Type            : TransactionType
  └─ CategoryIds[*]  : int  ─────────► Category.Id

Category
  ├─ Id              : int
  └─ Type            : CategoryType

ReportRequest
  ├─ Type            : ReportType
  └─ Data            : JsonElement
                          ├─ if Type=Period  → PeriodReportData  { Start: DateOnly, End: DateOnly }
                          └─ if Type=IsoWeek → (deferred — IsoWeekReportData ships with the IsoWeek strategy)

ReportResult
  ├─ Type             : ReportType
  ├─ Period           : string                  (descriptor; format per ReportType)
  ├─ IncomeTotal      : decimal (≥ 0)
  ├─ ExpenseTotal     : decimal (≥ 0)
  ├─ NetTotal         : decimal (signed)
  └─ CategoryBreakdown[*]  : CategoryBreakdownItem { Category: string, Total: signed decimal }
```

`Transaction → Category` is a many-to-many reference by id. The aggregate root is `Transaction` (it owns the reference list); `Category` is a value the transaction points at.

---

## Validation rules (deferred to downstream features)

Listed here for cross-reference only — none are enforced by the contracts in this feature. Each maps to a spec FR.

| Rule | Where enforced | Spec |
|---|---|---|
| `Amount > 0` | Future validation layer | FR-002 |
| `Description` non-empty | Future validation layer | FR-001 |
| `CategoryIds` non-empty | Future validation layer | FR-001 |
| Category name case-insensitive duplicate rejection | Future repository layer | FR-007 |
| Every `Transaction.CategoryIds` entry resolves to a known `Category.Id` | Future repository / service layer | (implied) |
| Every attached category's `Type` is compatible with the transaction's `Type` | Future validation layer | FR-020 / Assumptions |
| `Period.Start ≤ End` | Future `PeriodReportStrategy` | FR-012 / FR-029 |
| Unknown `ReportType` enum value (when binding) | Future model binding | FR-006 / FR-029 |
| `IsoWeek` requests have no registered strategy until the IsoWeek feature ships | Future `ReportStrategyFactory` | (MVP scope — see scope note above the `ReportType` enum) |

---

## State transitions

None for the MVP. All entities and DTOs are immutable on construction. There are no lifecycle states (no `Pending` / `Confirmed` / `Reversed` transaction states), no soft-delete flags, and no audit trail. Mutation requires constructing a new record with `with` expressions — which the spec deliberately leaves out of scope.
