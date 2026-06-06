namespace Finance.Mcp.Schema;

public record AgentResult(
    string ActionId,
    IReadOnlyList<ProposedChange> ProposedChanges,
    string Explanation,
    DateTime ReceivedAt);
