# Data Model: Transaction Export (JSON / CSV)

## New Entities / DTOs

None. The export feature introduces no new domain entities, DTOs, or enums.

It reuses the existing `TransactionResponse` DTO from `Finance.Business/Dtos/Transactions/`:

```
TransactionResponse
├── Id            : int
├── Description   : string
├── Amount        : decimal          (always > 0; direction encoded by Type)
├── Type          : TransactionType  (Income | Expense)
├── Timestamp     : DateTime         (year–second precision, local clock)
└── CategoryIds   : IReadOnlyList<int>  (non-empty)
```

`CategoryIds` — the existing field carries one or more integer category references. In the CSV output these are serialised as a semicolon-delimited string within a single cell (e.g., `1;2` for two categories).

## New Production Classes

### `Finance.Business/Export/TransactionCsvFormatter.cs`

| Member | Signature | Notes |
|--------|-----------|-------|
| `Format` | `static string Format(IEnumerable<TransactionResponse> transactions)` | Returns complete CSV text (header + data rows). Empty collection → header row only. |

CSV column order: `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds`

Formatting rules:
- `Amount` — invariant-culture decimal, full precision (no thousands separator).
- `Timestamp` — `yyyy-MM-ddTHH:mm:ss` (ISO 8601, no timezone).
- `CategoryIds` — semicolon-delimited list of integers (`1` or `1;2;3`).
- RFC 4180 quoting — any field containing `,`, `"`, `\r`, or `\n` is wrapped in `"..."` with internal `"` doubled.

## Modified Production Classes

### `Finance.Api/Controllers/TransactionsController.cs`

New action added (no existing action modified):

| Action | Route | Method |
|--------|-------|--------|
| `Export` | `GET /api/transactions/export` | Reads `Accept` header → JSON or CSV or 406 |

Accept resolution order (highest quality first, per `GetTypedHeaders().Accept`):

| Accept value | Response |
|--------------|----------|
| `application/json` | 200 + JSON array + `Content-Disposition: attachment; filename=transactions.json` |
| `text/csv` | 200 + CSV text (UTF-8) + `Content-Disposition: attachment; filename=transactions.csv` |
| `*/*` (or absent) | Defaults to JSON (same as `application/json` row above) |
| Anything else only | 406 Not Acceptable (empty body) |

## New Test Classes

| Test Class | Project | Covers |
|------------|---------|--------|
| `Finance.Business.UnitTests/Export/TransactionCsvFormatterTests.cs` | `Finance.Business.UnitTests` | RFC 4180 quoting, multi-category cell, empty list, amount/timestamp formatting |
| `Finance.Api.IntegrationTests/Controllers/TransactionsExportTests.cs` | `Finance.Api.IntegrationTests` | JSON export (200 + Content-Type + Content-Disposition), CSV export (same), */* → JSON, 406 for unsupported type |

No new test projects are introduced.
