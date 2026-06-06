namespace Finance.Mcp.Schema;

public record IterationLogEntry(
    string Timestamp,
    string Prompt,
    string ContextSnapshotHash,
    string ModelName,
    string AgentOutput,
    string? AcceptedDiff,
    string DecisionReason);
