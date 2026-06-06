namespace Finance.Mcp.Schema;

public record AgentActionRequest(
    string ActionId,
    string ContextId,
    ActionType ActionType,
    int IterationNumber,
    string PromptText,
    IReadOnlyDictionary<string, object> ContextFields);
