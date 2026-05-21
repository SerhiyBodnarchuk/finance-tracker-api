using System.Text.Json.Serialization;
using Finance.Data.Models;

namespace Finance.Business.Dtos.Transactions;

public sealed record TransactionResponse(
    int Id,
    DateTime Timestamp,
    string Description,
    decimal Amount,
    [property: JsonPropertyName("transactionType")] TransactionType Type,
    IReadOnlyList<CategorySummary> Categories);
