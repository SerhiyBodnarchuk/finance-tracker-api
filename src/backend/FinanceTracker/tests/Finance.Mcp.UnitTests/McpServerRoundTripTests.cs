using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;
using Moq;
using Xunit;

namespace Finance.Mcp.UnitTests;

public class McpServerRoundTripTests
{
    private static McpServer BuildServer(
        IMcpContextStore? store = null,
        IContextRedactor? redactor = null,
        IMcpIterationLogger? logger = null,
        ICategoryService? categoryService = null,
        int maxIterations = 3)
    {
        store ??= new McpContextStore();
        redactor ??= new ContextRedactor();
        logger ??= Mock.Of<IMcpIterationLogger>();
        categoryService ??= StubCategoryService();
        return new McpServer(store, redactor, logger, categoryService, Mock.Of<ITransactionService>(), maxIterations);
    }

    private static ICategoryService StubCategoryService()
    {
        var mock = new Mock<ICategoryService>();
        mock.Setup(s => s.GetAll()).Returns(new[]
        {
            new CategoryResponse(1, "Salary", CategoryType.Income),
            new CategoryResponse(2, "Groceries", CategoryType.Expense)
        });
        return mock.Object;
    }

    private static (
        IReadOnlyList<PendingTransactionItem> transactions,
        IReadOnlyList<int> ambiguousIds)
        ValidPayload()
    {
        var transactions = new[]
        {
            new PendingTransactionItem(10, 47.50m, "Expense", new DateTime(2026, 5, 28), "shop A"),
            new PendingTransactionItem(11, 120.00m, "Expense", new DateTime(2026, 5, 29), "shop B")
        };
        var ambiguousIds = new[] { 10, 11 };
        return (transactions, ambiguousIds);
    }

    // US1 scenario 1: sendContext returns a context ID and context is retrievable
    [Fact]
    public void SendContext_ValidPayload_ReturnsNonEmptyId()
    {
        var store = new McpContextStore();
        var server = BuildServer(store: store);
        var (txns, ids) = ValidPayload();

        var contextId = server.SendContext(txns, ids);

        Assert.NotEmpty(contextId);
        Assert.NotNull(store.Get(contextId));
    }

    // US1 scenario 2: requestAction returns a populated action request
    [Fact]
    public void RequestAction_ActiveContext_ReturnsActionRequest()
    {
        var server = BuildServer();
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);

        var action = server.RequestAction(contextId, ActionType.Categorize);

        Assert.NotEmpty(action.ActionId);
        Assert.Equal(contextId, action.ContextId);
        Assert.Equal(ActionType.Categorize, action.ActionType);
        Assert.Equal(1, action.IterationNumber);
    }

    // US1 scenario 3: receiveResult associates result with context
    [Fact]
    public void ReceiveResult_ValidActionId_Succeeds()
    {
        var server = BuildServer();
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);

        var result = new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(10, "categoryIds", "2") },
            "Assign groceries",
            DateTime.UtcNow);

        server.ReceiveResult(result); // must not throw
    }

    // US1 scenario 4: confirm transitions context to Confirmed
    [Fact]
    public void Confirm_NonBusinessLogicChange_TransitionsToConfirmed()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var server = BuildServer(logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(10, "categoryIds", "2") },
            "ok", DateTime.UtcNow));

        var result = server.Confirm(contextId);

        Assert.False(result.RequiresApproval);
        Assert.Equal(ContextStatus.Confirmed, result.Status);
        loggerMock.Verify(l => l.Log(It.IsAny<IterationLogEntry>()), Times.Once);
    }

    // US1 scenario 5: rollback transitions context to RolledBack
    [Fact]
    public void Rollback_ActiveContext_TransitionsToRolledBack()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var server = BuildServer(logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);

        server.Rollback(contextId, "test rollback");

        Assert.Throws<InvalidOperationException>(
            () => server.RequestAction(contextId, ActionType.Categorize));
        loggerMock.Verify(l => l.Log(It.Is<IterationLogEntry>(e => e.AcceptedDiff == null)), Times.Once);
    }

    // Edge case: confirm is idempotent on already-confirmed context
    [Fact]
    public void Confirm_AlreadyConfirmed_ReturnsSuccessIdempotently()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var server = BuildServer(logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(action.ActionId, Array.Empty<ProposedChange>(), "ok", DateTime.UtcNow));
        server.Confirm(contextId);

        var second = server.Confirm(contextId);

        Assert.Equal(ContextStatus.Confirmed, second.Status);
    }

    // Edge case: rollback on confirmed context throws
    [Fact]
    public void Rollback_ConfirmedContext_Throws()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var server = BuildServer(logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(action.ActionId, Array.Empty<ProposedChange>(), "ok", DateTime.UtcNow));
        server.Confirm(contextId);

        Assert.Throws<InvalidOperationException>(() => server.Rollback(contextId, "too late"));
    }

    // Edge case: non-ambiguous transaction without CategoryIds is rejected at sendContext
    [Fact]
    public void SendContext_NonAmbiguousTransactionWithoutCategoryIds_Throws()
    {
        var server = BuildServer();
        var txns = new[]
        {
            new PendingTransactionItem(10, 47.50m, "Expense", DateTime.UtcNow, "ambiguous"),
            new PendingTransactionItem(11, 20.00m, "Expense", DateTime.UtcNow, "non-ambiguous, no categories")
        };

        Assert.Throws<ArgumentException>(() => server.SendContext(txns, new[] { 10 }));
    }

    // Non-ambiguous transactions (with pre-set CategoryIds) are saved on confirm
    [Fact]
    public void Confirm_NonAmbiguousTransactionsAreSavedWithProvidedCategories()
    {
        var txServiceMock = new Mock<ITransactionService>();
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));

        var server = new McpServer(
            new McpContextStore(), new ContextRedactor(), loggerMock.Object,
            StubCategoryService(), txServiceMock.Object);

        var txns = new[]
        {
            new PendingTransactionItem(10, 47.50m, "Expense", DateTime.UtcNow, "ambiguous"),
            new PendingTransactionItem(11, 20.00m, "Expense", DateTime.UtcNow, "non-ambiguous", CategoryIds: new[] { 2 })
        };
        var contextId = server.SendContext(txns, new[] { 10 });
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(10, "categoryId", "2") },
            "Groceries", DateTime.UtcNow));

        server.Confirm(contextId);

        txServiceMock.Verify(s => s.Create(It.IsAny<Finance.Business.Dtos.Transactions.TransactionCreateRequest>()), Times.Exactly(2));
    }

    // Flow 4: empty ambiguousIds — all pre-categorised transactions saved on immediate confirm
    [Fact]
    public void SendContext_EmptyAmbiguousIds_AllTransactionsSavedOnConfirm()
    {
        var txServiceMock = new Mock<ITransactionService>();
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));

        var server = new McpServer(
            new McpContextStore(), new ContextRedactor(), loggerMock.Object,
            StubCategoryService(), txServiceMock.Object);

        var txns = new[]
        {
            new PendingTransactionItem(101, 200.00m, "Income",  DateTime.UtcNow, "Consulting invoice", CategoryIds: new[] { 1 }),
            new PendingTransactionItem(102,  30.00m, "Expense", DateTime.UtcNow, "Bus pass",           CategoryIds: new[] { 2 }),
            new PendingTransactionItem(103,  15.00m, "Expense", DateTime.UtcNow, "Spotify",            CategoryIds: new[] { 2 })
        };

        var contextId = server.SendContext(txns, Array.Empty<int>());
        var result = server.Confirm(contextId);

        Assert.False(result.RequiresApproval);
        Assert.Equal(ContextStatus.Confirmed, result.Status);
        txServiceMock.Verify(s => s.Create(It.IsAny<Finance.Business.Dtos.Transactions.TransactionCreateRequest>()), Times.Exactly(3));
    }

    // Edge case: sendContext rejects empty pending transactions
    [Fact]
    public void SendContext_EmptyTransactions_Throws()
    {
        var server = BuildServer();
        Assert.Throws<ArgumentException>(() =>
            server.SendContext(
                Array.Empty<PendingTransactionItem>(),
                new[] { 1 }));
    }

    // Edge case: sendContext rejects ambiguous IDs not in pending list
    [Fact]
    public void SendContext_AmbiguousIdNotInPendingList_Throws()
    {
        var server = BuildServer();
        var txns = new[] { new PendingTransactionItem(10, 1m, "Expense", DateTime.UtcNow, "x") };
        Assert.Throws<ArgumentException>(() =>
            server.SendContext(txns, new[] { 99 }));
    }

    // Approval gate: confirm with business-logic change requires approval
    [Fact]
    public void Confirm_BusinessLogicChange_RequiresApproval()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var server = BuildServer(logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(10, "categoryType", "Income") },
            "change type", DateTime.UtcNow));

        var result = server.Confirm(contextId);

        Assert.True(result.RequiresApproval);
        Assert.Equal(ContextStatus.PendingApproval, result.Status);
    }

    // ApproveConfirm completes a pending-approval context
    [Fact]
    public void ApproveConfirm_PendingApprovalContext_Confirms()
    {
        var loggerMock = new Mock<IMcpIterationLogger>(MockBehavior.Strict);
        loggerMock.Setup(l => l.Log(It.IsAny<IterationLogEntry>()));
        var store = new McpContextStore();
        var server = BuildServer(store: store, logger: loggerMock.Object);
        var (txns, ids) = ValidPayload();
        var contextId = server.SendContext(txns, ids);
        var action = server.RequestAction(contextId, ActionType.Categorize);
        server.ReceiveResult(new AgentResult(
            action.ActionId,
            new[] { new ProposedChange(10, "categoryType", "Income") },
            "change type", DateTime.UtcNow));
        server.Confirm(contextId);

        server.ApproveConfirm(contextId);

        Assert.Equal(ContextStatus.Confirmed, store.Get(contextId)?.Status);
    }
}
