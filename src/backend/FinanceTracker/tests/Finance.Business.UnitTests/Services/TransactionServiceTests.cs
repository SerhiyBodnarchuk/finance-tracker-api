using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Data.Repositories;
using Moq;
using Xunit;

namespace Finance.Business.UnitTests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _transactions = new(MockBehavior.Strict);
    private readonly Mock<ICategoryRepository> _categories = new(MockBehavior.Strict);

    private static readonly Category Groceries = new(2, "Groceries", CategoryType.Expense);
    private static readonly Category Transport = new(3, "Transport", CategoryType.Expense);

    private TransactionService NewService() => new(_transactions.Object, _categories.Object);

    [Fact]
    public void GetAll_maps_every_repository_transaction_to_response_with_resolved_categories()
    {
        var stored = new Transaction(
            1, new DateTime(2026, 5, 5, 12, 0, 0), "Silpo Market", 32.10m,
            TransactionType.Expense, new[] { Groceries.Id });
        _transactions.Setup(r => r.GetAll()).Returns(new[] { stored });
        _categories.Setup(r => r.GetAll()).Returns(new[] { Groceries });

        var result = NewService().GetAll();

        Assert.Single(result);
        var response = result.First();
        Assert.Equal(1, response.Id);
        Assert.Equal("Silpo Market", response.Description);
        Assert.Single(response.Categories);
        Assert.Equal("Groceries", response.Categories[0].Name);
    }

    [Fact]
    public void GetById_returns_mapped_response_with_resolved_categories()
    {
        var stored = new Transaction(
            3, new DateTime(2026, 5, 5, 12, 0, 0), "Silpo Market", 32.10m,
            TransactionType.Expense, new[] { Groceries.Id });
        _transactions.Setup(r => r.GetById(3)).Returns(stored);
        _categories.Setup(r => r.GetAll()).Returns(new[] { Groceries });

        var result = NewService().GetById(3);

        Assert.NotNull(result);
        Assert.Equal("Silpo Market", result!.Description);
        Assert.Equal("Groceries", result.Categories[0].Name);
    }

    [Fact]
    public void GetById_returns_null_when_repository_misses()
    {
        _transactions.Setup(r => r.GetById(9999)).Returns((Transaction?)null);

        var result = NewService().GetById(9999);

        Assert.Null(result);
        _categories.Verify(r => r.GetAll(), Times.Never);
    }

    [Fact]
    public void Create_passes_entity_to_repository_and_maps_returned_record()
    {
        var stored = new Transaction(
            6, new DateTime(2026, 5, 31, 12, 0, 0), "Coffee", 4.50m,
            TransactionType.Expense, new[] { Groceries.Id });
        _transactions.Setup(r => r.Add(It.IsAny<Transaction>())).Returns(stored);
        _categories.Setup(r => r.GetAll()).Returns(new[] { Groceries });

        var result = NewService().Create(new TransactionCreateRequest(
            Timestamp: new DateTime(2026, 5, 31, 12, 0, 0),
            Description: "Coffee",
            Amount: 4.50m,
            Type: TransactionType.Expense,
            CategoryIds: new[] { Groceries.Id }));

        Assert.Equal(6, result.Id);
        Assert.Equal("Coffee", result.Description);
        _transactions.Verify(r => r.Add(It.Is<Transaction>(t =>
            t.Description == "Coffee" && t.Amount == 4.50m && t.Type == TransactionType.Expense)), Times.Once);
    }

    [Fact]
    public void Delete_delegates_to_repository_and_returns_true_when_record_removed()
    {
        _transactions.Setup(r => r.Delete(4)).Returns(true);

        var result = NewService().Delete(4);

        Assert.True(result);
        _transactions.Verify(r => r.Delete(4), Times.Once);
    }

    [Fact]
    public void Delete_returns_false_when_repository_reports_miss()
    {
        _transactions.Setup(r => r.Delete(9999)).Returns(false);

        var result = NewService().Delete(9999);

        Assert.False(result);
    }
}
