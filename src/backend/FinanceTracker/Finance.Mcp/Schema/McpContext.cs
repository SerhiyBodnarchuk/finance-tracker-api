namespace Finance.Mcp.Schema;

public record McpContext(
    string Id,
    int Version,
    DateTime Ttl,
    IReadOnlyList<CategoryMappingItem> CategoryMappings,
    IReadOnlyList<PendingTransactionItem> PendingTransactions,
    ReconciliationContext Reconciliation,
    ContextStatus Status,
    DateTime CreatedAt)
{
    public McpContext WithStatus(ContextStatus status) => this with { Status = status };

    public McpContext WithReconciliation(ReconciliationContext reconciliation) =>
        this with { Reconciliation = reconciliation };
}
