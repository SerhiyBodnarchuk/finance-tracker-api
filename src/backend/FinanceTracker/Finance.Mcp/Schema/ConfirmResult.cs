namespace Finance.Mcp.Schema;

public record ConfirmResult(bool RequiresApproval, ContextStatus Status);
