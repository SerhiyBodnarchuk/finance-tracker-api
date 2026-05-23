using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Services.Reports;
using Finance.Data.Models;
using Finance.Data.Repositories;
using Moq;
using Xunit;

namespace Finance.Business.UnitTests.Services.Reports;

public class PeriodReportStrategyTests
{
    private readonly Mock<ITransactionRepository> _transactions = new(MockBehavior.Strict);
    private readonly Mock<ICategoryRepository> _categories = new(MockBehavior.Strict);

    private static readonly Category Salary        = new(1, "Salary",        CategoryType.Income);
    private static readonly Category Groceries     = new(2, "Groceries",     CategoryType.Expense);
    private static readonly Category Transport     = new(3, "Transport",     CategoryType.Expense);
    private static readonly Category Entertainment = new(4, "Entertainment", CategoryType.Expense);
    private static readonly Category Utilities     = new(5, "Utilities",     CategoryType.Expense);

    private static readonly IReadOnlyList<Category> SeededCategories =
        new[] { Salary, Groceries, Transport, Entertainment, Utilities };

    private static readonly IReadOnlyList<Transaction> SeededTransactions = new[]
    {
        new Transaction(1, new DateTime(2026, 5,  1, 12, 0, 0), "Monthly salary",       1200.00m, TransactionType.Income,  new[] { 1 }),
        new Transaction(2, new DateTime(2026, 5,  4, 12, 0, 0), "Uber Trip",              14.20m, TransactionType.Expense, new[] { 3 }),
        new Transaction(3, new DateTime(2026, 5,  5, 12, 0, 0), "Silpo Market",           32.10m, TransactionType.Expense, new[] { 2 }),
        new Transaction(4, new DateTime(2026, 5,  6, 12, 0, 0), "Netflix Subscription",    9.99m, TransactionType.Expense, new[] { 4 }),
        new Transaction(5, new DateTime(2026, 5, 10, 12, 0, 0), "Electricity Bill",       65.00m, TransactionType.Expense, new[] { 5 }),
    };

    private PeriodReportStrategy NewStrategy(
        IReadOnlyList<Transaction>? transactions = null,
        IReadOnlyList<Category>? categories = null)
    {
        _transactions.Setup(r => r.GetAll()).Returns(transactions ?? SeededTransactions);
        _categories.Setup(r => r.GetAll()).Returns(categories ?? SeededCategories);
        return new PeriodReportStrategy(_transactions.Object, _categories.Object);
    }

    private static ReportRequest PeriodRequest(string start, string end)
    {
        var data = JsonSerializer.SerializeToElement(new { start, end }, JsonSerializationOptions.Default);
        return new ReportRequest(ReportType.Period, data);
    }

    [Fact]
    public void Generates_summary_for_full_seed_window_with_documented_numbers()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(PeriodRequest("2026-05-01", "2026-05-31"));

        Assert.Equal(ReportType.Period, result.Type);
        Assert.Equal("2026-05-01..2026-05-31", result.Period);
        Assert.Equal(1200.00m, result.IncomeTotal);
        Assert.Equal(121.29m, result.ExpenseTotal);
        Assert.Equal(1078.71m, result.NetTotal);
        Assert.Equal(5, result.CategoryBreakdown.Count);
    }

    [Fact]
    public void Returns_zero_totals_and_empty_breakdown_for_empty_window()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(PeriodRequest("2030-01-01", "2030-01-31"));

        Assert.Equal(0m, result.IncomeTotal);
        Assert.Equal(0m, result.ExpenseTotal);
        Assert.Equal(0m, result.NetTotal);
        Assert.Empty(result.CategoryBreakdown);
    }

    [Fact]
    public void Includes_transaction_on_exact_start_boundary()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(PeriodRequest("2026-05-01", "2026-05-01"));

        Assert.Equal(1200.00m, result.IncomeTotal);
    }

    [Fact]
    public void Includes_transaction_on_exact_end_boundary()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(PeriodRequest("2026-05-10", "2026-05-10"));

        Assert.Equal(65.00m, result.ExpenseTotal);
    }

    [Fact]
    public void Multi_category_transaction_contributes_full_amount_to_each_category()
    {
        var splitBill = new Transaction(
            Id: 1,
            Timestamp: new DateTime(2026, 5, 15, 12, 0, 0),
            Description: "Grocery + Transport split bill",
            Amount: 50m,
            Type: TransactionType.Expense,
            CategoryIds: new[] { 2, 3 });
        var strategy = NewStrategy(
            transactions: new[] { splitBill },
            categories: new[] { Groceries, Transport });

        var result = strategy.Generate(PeriodRequest("2026-05-01", "2026-05-31"));

        Assert.Equal(0m, result.IncomeTotal);
        Assert.Equal(50m, result.ExpenseTotal);
        Assert.Equal(-50m, result.NetTotal);

        Assert.Equal(2, result.CategoryBreakdown.Count);
        Assert.All(result.CategoryBreakdown, item => Assert.Equal(-50m, item.Total));

        var sum = result.CategoryBreakdown.Sum(item => item.Total);
        Assert.Equal(-100m, sum);
    }

    [Fact]
    public void Breakdown_sort_order_is_income_first_then_alphabetical()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(PeriodRequest("2026-05-01", "2026-05-31"));

        var categoriesInOrder = result.CategoryBreakdown.Select(item => item.Category).ToArray();
        Assert.Equal(new[] { "Salary", "Entertainment", "Groceries", "Transport", "Utilities" }, categoriesInOrder);
    }

    [Fact]
    public void Throws_ReportValidationException_when_start_greater_than_end()
    {
        // No GetAll() setups - strict mock proves the strategy short-circuits before touching the repositories.
        var strategy = new PeriodReportStrategy(_transactions.Object, _categories.Object);

        var ex = Assert.Throws<ReportValidationException>(() =>
            strategy.Generate(PeriodRequest("2026-05-31", "2026-05-01")));

        Assert.Contains(ex.Errors, e => e.Message.Contains("less than or equal", StringComparison.OrdinalIgnoreCase));
        _transactions.Verify(r => r.GetAll(), Times.Never);
    }
}
