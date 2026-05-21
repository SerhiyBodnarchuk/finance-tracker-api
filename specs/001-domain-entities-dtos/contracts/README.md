# HTTP contracts

JSON-Schema view of every DTO that crosses the `Finance.Api` ↔ HTTP-consumer boundary. These are the *wire* contracts; the normative C# signatures live in [../data-model.md](../data-model.md).

| Schema | DTO | Used by future endpoint |
|---|---|---|
| [transaction-create-request.schema.json](transaction-create-request.schema.json) | `Finance.Business.Dtos.Transactions.TransactionCreateRequest` | `POST /api/transactions` request body |
| [transaction-response.schema.json](transaction-response.schema.json) | `Finance.Business.Dtos.Transactions.TransactionResponse` | `GET /api/transactions[/{id}]` response, and nested inside future report responses if those are reintroduced |
| [category-create-request.schema.json](category-create-request.schema.json) | `Finance.Business.Dtos.Categories.CategoryCreateRequest` | `POST /api/categories` request body |
| [category-response.schema.json](category-response.schema.json) | `Finance.Business.Dtos.Categories.CategoryResponse` | `GET /api/categories[/{id}]` response |
| [report-request.schema.json](report-request.schema.json) | `Finance.Business.Dtos.Reports.ReportRequest` (+ `PeriodReportData`, `IsoWeekReportData`) | `POST /api/reports` request body |
| [report-result.schema.json](report-result.schema.json) | `Finance.Business.Dtos.Reports.ReportResult` (+ `CategoryBreakdownItem`) | `POST /api/reports` response body |

JSON Schemas use **Draft 2020-12** so we can use `unevaluatedProperties: false` to lock down the wire shape and `if/then` to model the `ReportRequest.data` discriminated union without leaving the schema language.

These schemas are *not* used at runtime by `Finance.Api` (request binding goes through `System.Text.Json` and the records' positional constructors). They are the source of truth that the future OpenAPI document (when controllers ship) MUST match field-for-field.
