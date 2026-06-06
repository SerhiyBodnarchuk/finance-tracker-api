using Finance.Mcp.Schema;

namespace Finance.Mcp.Client;

/// <summary>
/// Thin facade over IMcpServer. Only uses Finance.Mcp schema types at its boundary —
/// no Finance.Business DTOs or Finance.Data entities are exposed here.
/// </summary>
public interface IMcpClient
{
    string SendContext(
        IReadOnlyList<PendingTransactionItem> pendingTransactions,
        IReadOnlyList<int> ambiguousIds,
        int? ttlMinutes = null);

    AgentActionRequest RequestAction(string contextId, ActionType actionType);

    void ReceiveResult(AgentResult result);

    ConfirmResult Confirm(string contextId);

    void Rollback(string contextId, string reason);
}
