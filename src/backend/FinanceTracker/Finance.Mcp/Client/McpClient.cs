using Finance.Mcp.Schema;

namespace Finance.Mcp.Client;

public class McpClient : IMcpClient
{
    private readonly IMcpServer _server;

    public McpClient(IMcpServer server) => _server = server;

    public string SendContext(
        IReadOnlyList<PendingTransactionItem> pendingTransactions,
        IReadOnlyList<int> ambiguousIds,
        int? ttlMinutes = null) =>
        _server.SendContext(pendingTransactions, ambiguousIds, ttlMinutes);

    public AgentActionRequest RequestAction(string contextId, ActionType actionType) =>
        _server.RequestAction(contextId, actionType);

    public void ReceiveResult(AgentResult result) =>
        _server.ReceiveResult(result);

    public ConfirmResult Confirm(string contextId) =>
        _server.Confirm(contextId);

    public void Rollback(string contextId, string reason) =>
        _server.Rollback(contextId, reason);
}
