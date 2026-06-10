using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;
using Moq;
using Xunit;

namespace Finance.Mcp.UnitTests;

public class VerifyRefineLoopTests
{
    private static ICategoryService StubCategoryService()
    {
        var mock = new Mock<ICategoryService>();
        mock.Setup(s => s.GetAll()).Returns(new[]
        {
            new CategoryResponse(2, "Groceries", CategoryType.Expense)
        });
        return mock.Object;
    }

    private static (McpServer server, string contextId) Setup(
        int maxIterations = 3,
        IMcpIterationLogger? logger = null)
    {
        logger ??= Mock.Of<IMcpIterationLogger>();
        var server = new McpServer(new McpContextStore(), new ContextRedactor(), logger, StubCategoryService(), Mock.Of<ITransactionService>(), maxIterations);
        var txns = new[] { new PendingTransactionItem(10, 50m, "Expense", DateTime.UtcNow, "store") };
        var contextId = server.SendContext(txns, new[] { 10 });
        return (server, contextId);
    }

    // US4 scenario 1: first response invalid, second valid — exactly 2 iterations
    [Fact]
    public void Loop_FirstInvalidSecondValid_TwoIterationsAndConfirms()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var (server, contextId) = Setup(maxIterations: 3, logger: loggerMock.Object);

        // Iteration 1: propose a business-logic-touching change (triggers approval gate, not a "failure")
        // For the "invalid" scenario we simulate: first result has incompatible category type change
        // The loop logic: caller checks result and decides to re-request
        var action1 = server.RequestAction(contextId, ActionType.Categorize);
        Assert.Equal(1, action1.IterationNumber);

        // Caller decides first response is invalid, issues another RequestAction without receiving
        var action2 = server.RequestAction(contextId, ActionType.Categorize);
        Assert.Equal(2, action2.IterationNumber);

        // Caller decides second response is valid and receives it
        var validResult = new AgentResult(
            action2.ActionId,
            new[] { new ProposedChange(10, "categoryIds", "2") },
            "Assign groceries",
            DateTime.UtcNow);
        server.ReceiveResult(validResult);

        var confirmResult = server.Confirm(contextId);
        Assert.False(confirmResult.RequiresApproval);
        Assert.Equal(ContextStatus.Confirmed, confirmResult.Status);
    }

    // US4 scenario 2: all iterations fail — auto-rollback at max
    [Fact]
    public void Loop_MaxIterationsExceeded_AutoRollsBackAndLogsExhaustion()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var (server, contextId) = Setup(maxIterations: 2, logger: loggerMock.Object);

        // Exhaust 2 iterations without receiving a result
        server.RequestAction(contextId, ActionType.Categorize); // iteration 1
        server.RequestAction(contextId, ActionType.Categorize); // iteration 2

        // Third call exceeds max — triggers auto-rollback
        var exhaustedAction = server.RequestAction(contextId, ActionType.Categorize);

        Assert.Equal("[LOOP EXHAUSTED]", exhaustedAction.PromptText);
        Assert.True(exhaustedAction.ContextFields.ContainsKey("loopExhausted"));

        // Rollback log entry should have been written with "max iterations exceeded"
        loggerMock.Verify(
            l => l.Log(It.Is<IterationLogEntry>(e =>
                e.DecisionReason.Contains("max iterations exceeded"))),
            Times.Once);
    }

    // Max iterations is configurable
    [Fact]
    public void Loop_MaxIterations1_ExhaustsOnSecondRequest()
    {
        var (server, contextId) = Setup(maxIterations: 1);

        var action1 = server.RequestAction(contextId, ActionType.Categorize);
        Assert.Equal(1, action1.IterationNumber);
        Assert.NotEqual("[LOOP EXHAUSTED]", action1.PromptText);

        var action2 = server.RequestAction(contextId, ActionType.Categorize);
        Assert.Equal("[LOOP EXHAUSTED]", action2.PromptText);
    }
}
