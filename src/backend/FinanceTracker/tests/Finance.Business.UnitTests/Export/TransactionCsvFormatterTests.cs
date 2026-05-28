using Finance.Business.Dtos.Transactions;
using Finance.Business.Export;
using Finance.Data.Models;
using Xunit;

namespace Finance.Business.UnitTests.Export;

public class TransactionCsvFormatterTests
{
    private static readonly string ExpectedHeader = "Id,Description,Amount,Type,Timestamp,CategoryIds";

    private static TransactionResponse MakeTransaction(
        int id = 1,
        string description = "Test",
        decimal amount = 10.00m,
        TransactionType type = TransactionType.Expense,
        DateTime? timestamp = null,
        int[]? categoryIds = null)
    {
        var ts = timestamp ?? new DateTime(2026, 5, 1, 12, 0, 0);
        var cats = (categoryIds ?? new[] { 1 })
            .Select(cid => new CategorySummary(cid, $"Category{cid}"))
            .ToList();
        return new TransactionResponse(id, ts, description, amount, type, cats);
    }

    private static string[] SplitLines(string csv) =>
        csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void EmptyList_ReturnsHeaderRowOnly()
    {
        var result = TransactionCsvFormatter.Format([]);
        var lines = SplitLines(result);
        Assert.Single(lines);
        Assert.Equal(ExpectedHeader, lines[0]);
    }

    [Fact]
    public void SingleTransaction_SingleCategory_FormatsAllColumns()
    {
        var ts = new DateTime(2026, 5, 1, 12, 0, 0);
        var transaction = MakeTransaction(
            id: 1, description: "Salary", amount: 1200.00m,
            type: TransactionType.Income, timestamp: ts, categoryIds: new[] { 3 });

        var result = TransactionCsvFormatter.Format([transaction]);
        var lines = SplitLines(result);

        Assert.Equal(2, lines.Length);
        Assert.Equal(ExpectedHeader, lines[0]);
        Assert.Equal("1,Salary,1200.00,Income,2026-05-01T12:00:00,3", lines[1]);
    }

    [Fact]
    public void SingleTransaction_MultipleCategories_SemicolonDelimited()
    {
        var transaction = MakeTransaction(categoryIds: new[] { 2, 5 });

        var result = TransactionCsvFormatter.Format([transaction]);
        var dataRow = SplitLines(result)[1];

        Assert.EndsWith(",2;5", dataRow);
    }

    [Fact]
    public void Description_WithComma_IsRfc4180Quoted()
    {
        var transaction = MakeTransaction(description: "Salary, monthly");

        var result = TransactionCsvFormatter.Format([transaction]);
        var dataRow = SplitLines(result)[1];

        Assert.Contains("\"Salary, monthly\"", dataRow);
    }

    [Fact]
    public void Description_WithDoubleQuote_DoublesTheQuote()
    {
        var transaction = MakeTransaction(description: "He said \"hello\"");

        var result = TransactionCsvFormatter.Format([transaction]);
        var dataRow = SplitLines(result)[1];

        Assert.Contains("\"He said \"\"hello\"\"\"", dataRow);
    }

    [Fact]
    public void Amount_UsesInvariantCulture_NoLocaleDecimalSeparator()
    {
        var transaction = MakeTransaction(amount: 1234.56m);

        var result = TransactionCsvFormatter.Format([transaction]);
        var dataRow = SplitLines(result)[1];

        Assert.Contains("1234.56", dataRow);
    }

    [Fact]
    public void Timestamp_FormattedAsIso8601NoZone()
    {
        var ts = new DateTime(2026, 5, 15, 9, 30, 45);
        var transaction = MakeTransaction(timestamp: ts);

        var result = TransactionCsvFormatter.Format([transaction]);
        var dataRow = SplitLines(result)[1];

        Assert.Contains("2026-05-15T09:30:45", dataRow);
    }
}
