namespace Finance.Mcp.Schema;

public record ProposedChange(int TransactionId, string TargetField, object ProposedValue);
