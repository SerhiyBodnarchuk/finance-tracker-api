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

public class IsoWeekReportStrategyTests
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

    // 2026-W18 = Apr 27 (Mon) – May 3 (Sun)   -> contains the May 1 salary.
    // 2026-W19 = May 4 (Mon) – May 10 (Sun)  -> contains the four expense transactions.
    private static readonly IReadOnlyList<Transaction> SeededTransactions = new[]
    {
        new Transaction(1, new DateTime(2026, 5,  1, 12, 0, 0), "Monthly salary",       1200.00m, TransactionType.Income,  new[] { 1 }),
        new Transaction(2, new DateTime(2026, 5,  4, 12, 0, 0), "Uber Trip",              14.20m, TransactionType.Expense, new[] { 3 }),
        new Transaction(3, new DateTime(2026, 5,  5, 12, 0, 0), "Silpo Market",           32.10m, TransactionType.Expense, new[] { 2 }),
        new Transaction(4, new DateTime(2026, 5,  6, 12, 0, 0), "Netflix Subscription",    9.99m, TransactionType.Expense, new[] { 4 }),
        new Transaction(5, new DateTime(2026, 5, 10, 12, 0, 0), "Electricity Bill",       65.00m, TransactionType.Expense, new[] { 5 }),
    };

    private IsoWeekReportStrategy NewStrategy(
        IReadOnlyList<Transaction>? transactions = null,
        IReadOnlyList<Category>? categories = null)
    {
        _transactions.Setup(r => r.GetAll()).Returns(transactions ?? SeededTransactions);
        _categories.Setup(r => r.GetAll()).Returns(categories ?? SeededCategories);
        return new IsoWeekReportStrategy(_transactions.Object, _categories.Object);
    }

    private static ReportRequest IsoWeekRequest(string week)
    {
        var data = JsonSerializer.SerializeToElement(new { week }, JsonSerializationOptions.Default);
        return new ReportRequest(ReportType.IsoWeek, data);
    }

    [Fact]
    public void Generates_summary_for_2026_W19_with_documented_numbers()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(IsoWeekRequest("2026-W19"));

        Assert.Equal(ReportType.IsoWeek, result.Type);
        Assert.Equal("2026-W19", result.Period);
        Assert.Equal(0m, result.IncomeTotal);
        Assert.Equal(121.29m, result.ExpenseTotal);
        Assert.Equal(-121.29m, result.NetTotal);
        Assert.Equal(4, result.CategoryBreakdown.Count);
    }

    [Fact]
    public void Generates_summary_for_2026_W18_with_only_salary_in_window()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(IsoWeekRequest("2026-W18"));

        Assert.Equal(1200.00m, result.IncomeTotal);
        Assert.Equal(0m, result.ExpenseTotal);
        Assert.Equal(1200.00m, result.NetTotal);

        Assert.Single(result.CategoryBreakdown);
        Assert.Equal("Salary", result.CategoryBreakdown[0].Category);
        Assert.Equal(1200.00m, result.CategoryBreakdown[0].Total);
    }

    [Fact]
    public void Breakdown_sort_order_is_expense_alphabetical_when_no_income()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(IsoWeekRequest("2026-W19"));

        var categoriesInOrder = result.CategoryBreakdown.Select(item => item.Category).ToArray();
        Assert.Equal(new[] { "Entertainment", "Groceries", "Transport", "Utilities" }, categoriesInOrder);
    }

    [Fact]
    public void Period_descriptor_is_yyyy_Www()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(IsoWeekRequest("2026-W20"));

        Assert.Equal("2026-W20", result.Period);
    }

    [Fact]
    public void Returns_zero_totals_and_empty_breakdown_when_week_has_no_transactions()
    {
        var strategy = NewStrategy();

        var result = strategy.Generate(IsoWeekRequest("2030-W01"));

        Assert.Equal(0m, result.IncomeTotal);
        Assert.Equal(0m, result.ExpenseTotal);
        Assert.Equal(0m, result.NetTotal);
        Assert.Empty(result.CategoryBreakdown);
    }

    [Fact]
    public void Includes_transaction_on_exact_monday_start_boundary()
    {
        var strategy = NewStrategy();

        // 2026-W19 starts Monday May 4 00:00:00; the Uber Trip (May 4 12:00:00) must be included.
        var result = strategy.Generate(IsoWeekRequest("2026-W19"));

        Assert.Contains(result.CategoryBreakdown, item => item.Category == "Transport" && item.Total == -14.20m);
    }

    [Fact]
    public void Includes_transaction_on_exact_sunday_end_boundary()
    {
        var strategy = NewStrategy();

        // 2026-W19 ends Sunday May 10 23:59:59; the Electricity Bill (May 10 12:00:00) must be included.
        var result = strategy.Generate(IsoWeekRequest("2026-W19"));

        Assert.Contains(result.CategoryBreakdown, item => item.Category == "Utilities" && item.Total == -65.00m);
    }

    [Fact]
    public void Throws_ReportValidationException_for_malformed_week_string()
    {
        // No GetAll() setups - strict mock proves the strategy short-circuits before touching the repositories.
        var strategy = new IsoWeekReportStrategy(_transactions.Object, _categories.Object);

        var ex = Assert.Throws<ReportValidationException>(() =>
            strategy.Generate(IsoWeekRequest("2026-19")));

        Assert.Contains(ex.Errors, e => e.Field == "data.week");
        _transactions.Verify(r => r.GetAll(), Times.Never);
    }

    [Fact]
    public void Throws_ReportValidationException_for_out_of_range_week_number()
    {
        var strategy = new IsoWeekReportStrategy(_transactions.Object, _categories.Object);

        // 2026 only has 53 weeks in ISO 8601 if it ends on a Thursday — it doesn't (2026-12-31 is Thursday → so 53 weeks).
        // 2025 has 52 weeks; week 53 is invalid for 2025.
        var ex = Assert.Throws<ReportValidationException>(() =>
            strategy.Generate(IsoWeekRequest("2025-W53")));

        Assert.Contains(ex.Errors, e => e.Field == "data.week");
        _transactions.Verify(r => r.GetAll(), Times.Never);
    }

    [Fact]
    public void Throws_ReportValidationException_when_week_is_missing()
    {
        var strategy = new IsoWeekReportStrategy(_transactions.Object, _categories.Object);
        var emptyData = JsonSerializer.SerializeToElement(new { }, JsonSerializationOptions.Default);
        var request = new ReportRequest(ReportType.IsoWeek, emptyData);

        var ex = Assert.Throws<ReportValidationException>(() => strategy.Generate(request));

        Assert.Contains(ex.Errors, e => e.Field == "data.week");
        _transactions.Verify(r => r.GetAll(), Times.Never);
    }

    [Fact]
    public void Throws_ReportValidationException_for_invalid_payload_json_type()
    {
        var strategy = new IsoWeekReportStrategy(_transactions.Object, _categories.Object);
        var bogus = JsonSerializer.SerializeToElement(42);
        var request = new ReportRequest(ReportType.IsoWeek, bogus);

        var ex = Assert.Throws<ReportValidationException>(() => strategy.Generate(request));

        Assert.Contains(ex.Errors, e => e.Field == "data");
        _transactions.Verify(r => r.GetAll(), Times.Never);
    }
}
