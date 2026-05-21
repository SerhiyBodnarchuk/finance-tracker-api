using System.Text.Json.Serialization;
using Finance.Data.Models;

namespace Finance.Business.Dtos.Transactions;

public sealed record TransactionCreateRequest(
    DateTime Timestamp,
    string Description,
    decimal Amount,
    [property: JsonPropertyName("transactionType")] TransactionType Type,
    IReadOnlyList<int> CategoryIds);
