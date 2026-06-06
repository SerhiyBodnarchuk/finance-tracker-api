namespace Finance.Mcp.Schema;

public record ContextSnapshot(
    string ContextId,
    DateTime SerialisedAt,
    string Hash,
    string RedactedJson);
