namespace Finance.Data.Models;

public sealed record Transaction(
    int Id,
    DateTime Timestamp,
    string Description,
    decimal Amount,
    TransactionType Type,
    IReadOnlyList<int> CategoryIds);
