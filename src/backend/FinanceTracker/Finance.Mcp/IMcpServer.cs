using Finance.Mcp.Schema;

namespace Finance.Mcp;

public interface IMcpServer
{
    string SendContext(
        IReadOnlyList<PendingTransactionItem> pendingTransactions,
        IReadOnlyList<int> ambiguousIds,
        int? ttlMinutes = null);

    AgentActionRequest RequestAction(string contextId, ActionType actionType);

    void ReceiveResult(AgentResult result);

    ConfirmResult Confirm(string contextId);

    void Rollback(string contextId, string reason);

    void ApproveConfirm(string contextId);

    ContextSnapshot TakeSnapshot(string contextId);
}
