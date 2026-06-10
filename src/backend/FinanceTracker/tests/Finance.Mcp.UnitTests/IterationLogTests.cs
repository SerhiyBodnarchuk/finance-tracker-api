using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;
using Moq;
using Xunit;

namespace Finance.Mcp.UnitTests;

public class IterationLogTests : IDisposable
{
    private readonly string _logPath = Path.GetTempFileName();

    public void Dispose() => File.Delete(_logPath);

    private McpServer BuildServer()
    {
        var categorySvc = new Mock<ICategoryService>();
        categorySvc.Setup(s => s.GetAll()).Returns(new[]
        {
            new CategoryResponse(2, "Groceries", CategoryType.Expense)
        });
        return new McpServer(new McpContextStore(), new ContextRedactor(), new McpIterationLogger(_logPath), categorySvc.Object, Mock.Of<ITransactionService>());
    }

    private static (McpServer server, string contextId) SetupWithServer(McpServer server)
    {
        var txns = new[] { new PendingTransactionItem(1, 10m, "Expense", DateTime.UtcNow, "x") };
        var contextId = server.SendContext(txns, new[] { 1 });
        return (server, contextId);
    }

    // US5 scenario 1: confirm produces a log entry with all required FR-010 fields
    [Fact]
    public void Confirm_ProducesLogEntryWithAllRequiredFields()
    {
        var server = BuildServer();
        var (_, contextId) = SetupWithServer(server);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(1, "categoryIds", "2") },
            "assign food",
            DateTime.UtcNow));

        server.Confirm(contextId);

        var log = File.ReadAllText(_logPath);
        Assert.Contains("--- MCP ENTRY START ---", log);
        Assert.Contains("Timestamp:", log);
        Assert.Contains("Prompt:", log);
        Assert.Contains("ContextSnapshotHash:", log);
        Assert.Contains("ModelName:", log);
        Assert.Contains("AgentOutput:", log);
        Assert.Contains("AcceptedDiff:", log);
        Assert.Contains("DecisionReason:", log);
        Assert.Contains("--- MCP ENTRY END ---", log);
    }

    // US5 scenario 2: rollback produces an entry with AcceptedDiff null
    [Fact]
    public void Rollback_ProducesLogEntryWithNullAcceptedDiff()
    {
        var server = BuildServer();
        var (_, contextId) = SetupWithServer(server);

        server.Rollback(contextId, "test rejection");

        var log = File.ReadAllText(_logPath);
        Assert.Contains("AcceptedDiff: null", log);
        Assert.Contains("DecisionReason: test rejection", log);
    }

    // Retention: entries beyond cap are pruned on next write
    [Fact]
    public void Log_RetentionPrunesEntriesBeyondCap()
    {
        var logger = new McpIterationLogger(_logPath);
        var entry = new IterationLogEntry(
            DateTime.UtcNow.ToString("O"),
            "prompt", "hash", "model", "output", null, "reason");

        // Write cap+1 entries — last write should prune to cap
        const int overCap = 501;
        for (int i = 0; i < overCap; i++)
            logger.Log(entry);

        var content = File.ReadAllText(_logPath);
        var count = CountOccurrences(content, "--- MCP ENTRY START ---");
        Assert.True(count <= 500, $"Expected ≤ 500 entries after pruning but got {count}");
    }

    // Two entries: one confirm + one rollback = two log blocks
    [Fact]
    public void ConfirmThenRollback_TwoEntriesInLog()
    {
        var server = BuildServer();
        var (_, ctx1) = SetupWithServer(server);
        var action = server.RequestAction(ctx1, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(action.ActionId, Array.Empty<ProposedChange>(), "ok", DateTime.UtcNow));
        server.Confirm(ctx1);

        var (_, ctx2) = SetupWithServer(server);
        server.Rollback(ctx2, "bad result");

        var content = File.ReadAllText(_logPath);
        var count = CountOccurrences(content, "--- MCP ENTRY START ---");
        Assert.Equal(2, count);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, pos = 0;
        while ((pos = text.IndexOf(pattern, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += pattern.Length;
        }
        return count;
    }
}
