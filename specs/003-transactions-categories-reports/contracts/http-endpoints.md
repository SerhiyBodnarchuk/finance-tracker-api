# HTTP Contracts: Transactions / Categories Endpoints with Business Validation and Period Reports

**Feature**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md) | **Data Model**: [../data-model.md](../data-model.md)

This file is the **umbrella contract** for every HTTP endpoint introduced by this feature. The accompanying JSON Schema files in this folder pin down the request and response shapes. Status-code-level behavior is documented here.

All endpoints live under `/api/`. All accept and return `application/json`. Error responses are RFC 7807 `application/problem+json` `ProblemDetails` bodies; validation failures use the `ValidationProblemDetails` extension (the `errors` dictionary keyed by field name).

## Endpoint summary

| Method  | Path                       | Success | Failure modes                                                                  |
|---------|----------------------------|---------|--------------------------------------------------------------------------------|
| `GET`   | `/api/transactions`        | `200`   | (none)                                                                         |
| `GET`   | `/api/transactions/{id}`   | `200`   | `404` if no such id                                                            |
| `POST`  | `/api/transactions`        | `201`   | `400` for any validation failure (see FR-009..FR-013); `Location` set on `201` |
| `DELETE`| `/api/transactions/{id}`   | `204`   | `404` if no such id                                                            |
| `GET`   | `/api/categories`          | `200`   | (none)                                                                         |
| `GET`   | `/api/categories/{id}`     | `200`   | `404` if no such id                                                            |
| `POST`  | `/api/categories`          | `201`   | `400` for empty/whitespace `name`; `409` for case-insensitive duplicate name   |
| `POST`  | `/api/reports`             | `200`   | `400` for unknown / unsupported `type`; `400` for invalid `data` payload       |

## Transactions

### `GET /api/transactions`

- **Request**: no body.
- **Response 200**: JSON array of `TransactionResponse` objects in id-ascending order. May be empty.

### `GET /api/transactions/{id}`

- **Request**: no body. `id` is a positive integer path parameter.
- **Response 200**: `TransactionResponse` object.
- **Response 404**: `ProblemDetails` with `status: 404`, `title: "Transaction not found"`, `detail` naming the missing id.

### `POST /api/transactions`

- **Request body**: `TransactionCreateRequest` JSON. See `transactions-endpoints.schema.json`.
  - `timestamp` — ISO 8601 date-time, required, must parse.
  - `description` — string, required, non-empty after trim (FR-010).
  - `amount` — number, required, **strictly greater than zero** (FR-009).
  - `transactionType` — `"Income"` or `"Expense"`. Anything else fails model binding with HTTP 400.
  - `categoryIds` — non-empty array of positive integers (FR-011). Every id must resolve to an existing category (FR-012). Every referenced category's type must be compatible with `transactionType` (FR-013).
- **Response 201**: `TransactionResponse` body. `Location: /api/transactions/{newId}` header.
- **Response 400**: `ValidationProblemDetails`. The `errors` dictionary contains keys like `"amount"`, `"description"`, `"categoryIds"`, `"categoryIds[0]"`. Each key maps to a string array of error messages.

Example bad request:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "amount": ["Amount must be greater than zero."],
    "categoryIds[0]": ["Category id 9999 does not exist."]
  }
}
```

### `DELETE /api/transactions/{id}`

- **Request**: no body. `id` is a positive integer.
- **Response 204**: empty body.
- **Response 404**: `ProblemDetails` for unknown id.

## Categories

### `GET /api/categories`

- **Request**: no body.
- **Response 200**: JSON array of `CategoryResponse` objects in id-ascending order.

### `GET /api/categories/{id}`

- **Request**: no body.
- **Response 200**: `CategoryResponse` object.
- **Response 404**: `ProblemDetails` for unknown id.

### `POST /api/categories`

- **Request body**: `CategoryCreateRequest` JSON.
  - `name` — string, required, non-empty after trim (FR-014).
  - `type` — `"Income"`, `"Expense"`, or `"Both"`. Anything else fails model binding with HTTP 400.
- **Response 201**: `CategoryResponse` body. `Location: /api/categories/{newId}` header.
- **Response 400**: `ValidationProblemDetails` keyed by `"name"`.
- **Response 409**: `ProblemDetails` with `status: 409`, `title: "Duplicate category name"`, `detail: "A category named '{name}' already exists."` (FR-008). Duplicate check is case-insensitive (the repository layer enforces it; the controller catches `InvalidOperationException` and translates).

## Reports

### `POST /api/reports`

- **Request body**: `ReportRequest` envelope.
  - `type` — `"Period"` (only `Period` is currently supported; `"IsoWeek"` is a known enum value but the strategy is not yet registered; any other value fails model binding).
  - `data` — payload object whose shape depends on `type`. For `Period`: `{ start, end }` with both fields as ISO `yyyy-MM-dd` dates and `start <= end`.
- **Response 200**: `ReportResult` body. The response has exactly six fields — `type`, `period`, `incomeTotal`, `expenseTotal`, `netTotal`, `categoryBreakdown`. No `transactions` array, no `currency` field, no extras.
- **Response 400**:
  - For unknown `type` (e.g., `"Month"`): `ProblemDetails` with `title: "Unsupported report type"`, `detail` naming the type.
  - For `type: "IsoWeek"`: same shape — `title: "Unsupported report type"`, `detail` explaining that IsoWeek is not yet available (FR-018).
  - For missing / invalid `data` fields (missing `start`, missing `end`, non-parseable date, `start > end`): `ValidationProblemDetails`. Keys are `"data.start"`, `"data.end"`, or `"data"`.

#### `POST /api/reports` request example (Period)

```json
{
  "type": "Period",
  "data": {
    "start": "2026-05-01",
    "end": "2026-05-31"
  }
}
```

#### `POST /api/reports` 200 response example (over the seed window)

```json
{
  "type": "Period",
  "period": "2026-05-01..2026-05-31",
  "incomeTotal": 1200.00,
  "expenseTotal": 121.29,
  "netTotal": 1078.71,
  "categoryBreakdown": [
    { "category": "Salary",        "total":  1200.00 },
    { "category": "Entertainment", "total":    -9.99 },
    { "category": "Groceries",     "total":   -32.10 },
    { "category": "Transport",     "total":   -14.20 },
    { "category": "Utilities",     "total":   -65.00 }
  ]
}
```

Note the sort order: `Salary` (only income-side item) first; then expense-side items alphabetically by name (`Entertainment`, `Groceries`, `Transport`, `Utilities`). Each expense item carries a **negative** `total` because direction is encoded in the sign.

The arithmetic sum of `categoryBreakdown[*].total` is `1200.00 - 9.99 - 32.10 - 14.20 - 65.00 = 1078.71`, which equals `netTotal` because every seeded transaction is single-category. For a multi-category transaction the sum would differ from `netTotal` by the duplicated contribution (FR-025).

## Idempotency

- `GET` and `DELETE` are idempotent by HTTP convention.
- `POST /api/transactions` and `POST /api/categories` create one new record per call (no idempotency keys).
- `POST /api/reports` is **deterministic** but not strictly idempotent in the HTTP sense (no state change). Two identical calls with no intervening writes return byte-identical bodies (SC-004) — reports are ad-hoc and never cached.

## OpenAPI

The OpenAPI document at `/openapi/v1.json` (Development only, gated by `app.Environment.IsDevelopment()`) lists every endpoint above with its request and response schemas inferred from the C# DTO and controller signatures. Scalar's reference UI at `/scalar/v1` renders the same document. Both endpoints continue to be **not exposed in Release builds**.

## JSON Schema files in this folder

| File                                | Endpoints                                                   |
|-------------------------------------|-------------------------------------------------------------|
| `transactions-endpoints.schema.json`| `GET /api/transactions`, `GET /api/transactions/{id}`, `POST /api/transactions`, `DELETE /api/transactions/{id}` |
| `categories-endpoints.schema.json`  | `GET /api/categories`, `GET /api/categories/{id}`, `POST /api/categories` |
| `reports-endpoint.schema.json`      | `POST /api/reports`                                          |

Each schema file declares the request and response shapes referenced above. They're consumed by the OpenAPI generation step and by anyone reviewing this feature for contract-test coverage.
