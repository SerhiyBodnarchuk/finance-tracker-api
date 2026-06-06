using System.ComponentModel;
using System.Text.Json;
using Finance.Mcp.Schema;
using ModelContextProtocol.Server;
using FinanceMcpServer = Finance.Mcp.IMcpServer;

namespace Finance.Mcp.Host.Tools;

public record ProposedChangeRequest(int TransactionId, string TargetField, string ProposedValue);

[McpServerToolType]
public class FinanceMcpTools(FinanceMcpServer server)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [McpServerTool(Name = "sendContext")]
    [Description(
        "Send a reconciliation context to the MCP server. " +
        "Returns the context ID used in all subsequent calls. " +
        "Category mappings are resolved server-side from the Finance data. " +
        "pendingTransactions date format: ISO 8601 (e.g. '2026-01-15T09:00:00').")]
    public string SendContext(
        List<PendingTransactionItem> pendingTransactions,
        List<int> ambiguousIds,
        int? ttlMinutes = null)
    {
        try
        {
            return server.SendContext(pendingTransactions, ambiguousIds, ttlMinutes);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "requestAction")]
    [Description(
        "Request an agent action for a stored context. " +
        "actionType must be 'Categorize' or 'Validate'. " +
        "Returns an AgentActionRequest with actionId and promptText. " +
        "When PromptText is '[LOOP EXHAUSTED]' the verify-refine loop has been auto-rolled back.")]
    public string RequestAction(string contextId, string actionType)
    {
        try
        {
            if (!Enum.TryParse<ActionType>(actionType, ignoreCase: true, out var parsed))
                return $"ERROR: Unknown actionType '{actionType}'. Valid values: Categorize, Validate.";

            var result = server.RequestAction(contextId, parsed);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "receiveResult")]
    [Description(
        "Submit the agent's proposed category changes for a pending action. " +
        "proposedChanges is a list of {transactionId, targetField, proposedValue} objects. " +
        "proposedValue is always a string (e.g. the category id as '1').")]
    public string ReceiveResult(
        string actionId,
        List<ProposedChangeRequest> proposedChanges,
        string explanation)
    {
        try
        {
            var changes = proposedChanges
                .Select(c => new ProposedChange(c.TransactionId, c.TargetField, c.ProposedValue))
                .ToList()
                .AsReadOnly();

            server.ReceiveResult(new AgentResult(actionId, changes, explanation, DateTime.UtcNow));
            return "OK";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "confirm")]
    [Description(
        "Confirm the agent result for a context. " +
        "Returns {requiresApproval, status}. " +
        "If requiresApproval is true the context enters PendingApproval and needs approveConfirm.")]
    public string Confirm(string contextId)
    {
        try
        {
            var result = server.Confirm(contextId);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "rollback")]
    [Description("Roll back a context, discarding the agent result. Provide a short reason string.")]
    public string Rollback(string contextId, string reason)
    {
        try
        {
            server.Rollback(contextId, reason);
            return "OK";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "approveConfirm")]
    [Description(
        "Approve a context that is in PendingApproval state. " +
        "Only needed when confirm returned requiresApproval=true (business-logic field change).")]
    public string ApproveConfirm(string contextId)
    {
        try
        {
            server.ApproveConfirm(contextId);
            return "OK";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool(Name = "takeSnapshot")]
    [Description(
        "Take a redacted point-in-time snapshot of a context. " +
        "Returns {contextId, serialisedAt, hash, redactedJson}. " +
        "The hash is SHA-256 of the redacted JSON and is used for replay determinism.")]
    public string TakeSnapshot(string contextId)
    {
        try
        {
            var snapshot = server.TakeSnapshot(contextId);
            return JsonSerializer.Serialize(snapshot, Json);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}
