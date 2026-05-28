# Quickstart: Transaction Export (JSON / CSV)

## What ships in this feature

- `GET /api/transactions/export` — returns all transactions as JSON or CSV based on the `Accept` header.
- `TransactionCsvFormatter` — static formatter in `Finance.Business/Export/` that converts `IEnumerable<TransactionResponse>` to RFC 4180 CSV text.
- Unit tests for CSV formatting in `Finance.Business.UnitTests`.
- Integration tests for content negotiation in `Finance.Api.IntegrationTests`.

## Try it

Run the API:

```powershell
cd src/backend/FinanceTracker
dotnet run --project Finance.Api
```

### Fetch JSON

```powershell
Invoke-WebRequest -Uri "http://localhost:5182/api/transactions/export" `
  -Headers @{ Accept = "application/json" } | Select-Object -ExpandProperty Content
```

Expected: JSON array of all seeded transactions.

### Fetch CSV

```powershell
Invoke-WebRequest -Uri "http://localhost:5182/api/transactions/export" `
  -Headers @{ Accept = "text/csv" } -OutFile "transactions.csv"
```

Expected: `transactions.csv` saved to the current directory, openable in Excel.

### Trigger 406

```powershell
Invoke-RestMethod -Uri "http://localhost:5182/api/transactions/export" `
  -Headers @{ Accept = "application/xml" }
```

Expected: `406 Not Acceptable`.

## New files

| File | Layer | Purpose |
|------|-------|---------|
| `Finance.Business/Export/TransactionCsvFormatter.cs` | Business | Static CSV formatter |
| `Finance.Business.UnitTests/Export/TransactionCsvFormatterTests.cs` | Test | Unit tests for CSV formatter |
| `Finance.Api.IntegrationTests/Controllers/TransactionsExportTests.cs` | Test | Integration tests for content negotiation |

## Modified files

| File | Change |
|------|--------|
| `Finance.Api/Controllers/TransactionsController.cs` | Add `Export` action (`[HttpGet("export")]`) |

## Required test cases

### `TransactionCsvFormatterTests`

| Test | Asserts |
|------|---------|
| `EmptyList_ReturnsHeaderRowOnly` | Output contains exactly one row (header) |
| `SingleTransaction_SingleCategory_FormatsCorrectly` | All columns present; `CategoryIds` is a single integer string |
| `SingleTransaction_MultipleCategories_SemicolonDelimited` | `CategoryIds` cell is `"1;2"` |
| `DescriptionWithComma_IsQuoted` | Description cell wrapped in `"..."` |
| `DescriptionWithDoubleQuote_IsEscaped` | Internal `"` doubled in output |
| `Amount_FormattedWithInvariantCulture` | No locale-specific decimal separator |
| `Timestamp_FormattedAsIso8601` | Output matches `yyyy-MM-ddTHH:mm:ss` |

### `TransactionsExportTests` (integration)

| Test | Asserts |
|------|---------|
| `AcceptJson_Returns200WithJsonContentType` | 200, `Content-Type: application/json`, non-empty body |
| `AcceptJson_ContentDispositionIsAttachment` | `Content-Disposition: attachment; filename=transactions.json` |
| `AcceptCsv_Returns200WithCsvContentType` | 200, `Content-Type: text/csv`, body starts with header row |
| `AcceptCsv_ContentDispositionIsAttachment` | `Content-Disposition: attachment; filename=transactions.csv` |
| `AcceptWildcard_DefaultsToJson` | `Accept: */*` → 200 with `Content-Type: application/json` |
| `NoAcceptHeader_DefaultsToJson` | No `Accept` header → 200 with `Content-Type: application/json` |
| `AcceptXml_Returns406` | 406 Not Acceptable |
| `AcceptCsv_BodyContainsAllSeededTransactions` | Row count = seeded transaction count + 1 (header) |
