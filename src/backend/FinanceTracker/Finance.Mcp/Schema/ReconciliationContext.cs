namespace Finance.Mcp.Schema;

public record ReconciliationContext(
    IReadOnlyList<int> AmbiguousTransactionIds,
    IReadOnlyDictionary<int, IReadOnlyList<int>> SuggestedCategoryIds,
    ContextStatus DecisionStatus);
