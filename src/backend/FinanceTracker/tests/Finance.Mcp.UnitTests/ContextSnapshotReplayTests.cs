using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;
using Moq;
using Xunit;

namespace Finance.Mcp.UnitTests;

public class ContextSnapshotReplayTests
{
    private static McpServer BuildServer(IMcpContextStore? store = null)
    {
        store ??= new McpContextStore();
        var categorySvc = new Mock<ICategoryService>();
        categorySvc.Setup(s => s.GetAll()).Returns(new[]
        {
            new CategoryResponse(1, "Salary", CategoryType.Income),
            new CategoryResponse(2, "Groceries", CategoryType.Expense)
        });
        return new McpServer(store, new ContextRedactor(), Mock.Of<IMcpIterationLogger>(), categorySvc.Object, Mock.Of<ITransactionService>());
    }

    private static (IReadOnlyList<PendingTransactionItem>, IReadOnlyList<int>)
        FixedPayload() =>
        (
            new[]
            {
                new PendingTransactionItem(42, 47.50m, "Expense", new DateTime(2026, 5, 28, 9, 15, 0), "shop A"),
                new PendingTransactionItem(43, 120.00m, "Expense", new DateTime(2026, 5, 29, 14, 30, 0), "shop B")
            },
            new[] { 42, 43 }
        );

    // US2 scenario 1: three replays produce byte-identical proposed_changes and explanation
    [Fact]
    public void Replay_ThreeConsecutiveRuns_ByteIdenticalOutput()
    {
        static (string proposedChanges, string explanation) RunOnce()
        {
            var store = new McpContextStore();
            var server = BuildServer(store);
            var (txns, ids) = FixedPayload();
            var contextId = server.SendContext(txns, ids, ttlMinutes: 60);

            var action = server.RequestAction(contextId, ActionType.Categorize);

            // Deterministic mock agent: always returns the same response
            var result = new AgentResult(
                action.ActionId,
                new[] { new ProposedChange(42, "categoryIds", new[] { 1 }) },
                "Assign groceries category",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            server.ReceiveResult(result);
            return (
                string.Join(",", result.ProposedChanges.Select(c => $"{c.TransactionId}:{c.TargetField}")),
                result.Explanation
            );
        }

        var run1 = RunOnce();
        var run2 = RunOnce();
        var run3 = RunOnce();

        Assert.Equal(run1.proposedChanges, run2.proposedChanges);
        Assert.Equal(run1.proposedChanges, run3.proposedChanges);
        Assert.Equal(run1.explanation, run2.explanation);
        Assert.Equal(run1.explanation, run3.explanation);
    }

    // US2 scenario 2: snapshots of the same context content at different times produce identical hashes
    [Fact]
    public void TakeSnapshot_SameContextContent_IdenticalHashes()
    {
        var store = new McpContextStore();
        var server = BuildServer(store);
        var (txns, ids) = FixedPayload();
        var contextId = server.SendContext(txns, ids, ttlMinutes: 60);

        // Force a fixed TTL so the serialised content is stable
        var context = store.Get(contextId)!;
        var fixedTtl = new DateTime(2027, 12, 31, 15, 0, 0, DateTimeKind.Utc);
        store.Set(contextId, context with { Ttl = fixedTtl });

        var snap1 = server.TakeSnapshot(contextId);
        var snap2 = server.TakeSnapshot(contextId);

        Assert.Equal(snap1.Hash, snap2.Hash);
        Assert.Equal(snap1.RedactedJson, snap2.RedactedJson);
    }

    // Snapshot descriptions are always [REDACTED]
    [Fact]
    public void TakeSnapshot_DescriptionsAreRedacted()
    {
        var store = new McpContextStore();
        var server = BuildServer(store);
        var (txns, ids) = FixedPayload();
        var contextId = server.SendContext(txns, ids, ttlMinutes: 60);

        var snapshot = server.TakeSnapshot(contextId);

        Assert.DoesNotContain("shop A", snapshot.RedactedJson);
        Assert.DoesNotContain("shop B", snapshot.RedactedJson);
        Assert.Contains("[REDACTED]", snapshot.RedactedJson);
    }
}
