# HTTP Endpoint Contract: Transaction Export

## `GET /api/transactions/export`

Exports all transactions in the format requested by the client via the `Accept` header.

### Request

```
GET /api/transactions/export HTTP/1.1
Accept: application/json   ; or text/csv   ; or */*
```

No request body. No query parameters. No path parameters.

### Responses

#### 200 OK — JSON

```
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Content-Disposition: attachment; filename=transactions.json

[
  {
    "id": 1,
    "description": "Groceries",
    "amount": 52.30,
    "type": "Expense",
    "timestamp": "2026-05-01T12:30:00",
    "categoryIds": [2, 3]
  },
  ...
]
```

Triggered by: `Accept: application/json`, `Accept: */*`, or absent `Accept` header.

The array is the full transaction dataset (no pagination). Order is not guaranteed (insertion order is the expected default).

#### 200 OK — CSV

```
HTTP/1.1 200 OK
Content-Type: text/csv; charset=utf-8
Content-Disposition: attachment; filename=transactions.csv

Id,Description,Amount,Type,Timestamp,CategoryIds
1,Groceries,52.30,Expense,2026-05-01T12:30:00,2;3
2,"Salary, monthly",3500.00,Income,2026-05-01T09:00:00,1
```

Triggered by: `Accept: text/csv`.

Column order: `Id`, `Description`, `Amount`, `Type`, `Timestamp`, `CategoryIds`.

Formatting rules:
- RFC 4180: fields containing `,`, `"`, `\r`, or `\n` are wrapped in `"..."` with internal `"` doubled.
- `Amount`: invariant-culture decimal, full precision, no thousands separator.
- `Timestamp`: `yyyy-MM-ddTHH:mm:ss`.
- `CategoryIds`: semicolon-delimited list of integer IDs within the cell.
- Empty dataset: header row only (no data rows).

#### 406 Not Acceptable

```
HTTP/1.1 406 Not Acceptable
```

Triggered by: `Accept` header containing only unsupported media types (e.g., `application/xml`).

Empty body. No `ProblemDetails` payload.

### Quality-Weighted Negotiation

The endpoint resolves the format by iterating `Accept` header values in descending quality order (`q` parameter). The first entry matching a supported type wins. `*/*` matches both and defaults to `application/json`.

Example: `Accept: text/csv;q=0.5, application/json;q=1.0` → JSON wins (higher quality).

### Error Behaviour

This endpoint does not validate or filter the transaction dataset. It cannot produce `400 Bad Request` or `404 Not Found`. The only error response is `406 Not Acceptable`.
