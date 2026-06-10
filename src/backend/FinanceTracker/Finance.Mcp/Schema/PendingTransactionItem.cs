namespace Finance.Mcp.Schema;

public record PendingTransactionItem(
    int Id,
    decimal Amount,
    string TransactionType,
    DateTime Date,
    /// <summary>Always serialised as "[REDACTED]" in context snapshots and log entries.</summary>
    string Description,
    IReadOnlyList<int>? CategoryIds = null);
