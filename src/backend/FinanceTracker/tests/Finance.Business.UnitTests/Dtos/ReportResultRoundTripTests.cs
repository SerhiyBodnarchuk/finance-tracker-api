using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Xunit;

namespace Finance.Business.UnitTests.Dtos;

public class ReportResultRoundTripTests
{
    [Fact]
    public void Serializes_field_for_field()
    {
        var result = new ReportResult(
            Type: ReportType.Period,
            Period: "2026-05-01..2026-05-31",
            IncomeTotal: 1200.00m,
            ExpenseTotal: 430.50m,
            NetTotal: 769.50m,
            CategoryBreakdown: new[]
            {
                new CategoryBreakdownItem("Salary", 1200.00m),
                new CategoryBreakdownItem("Dining out", -65.25m),
                new CategoryBreakdownItem("Groceries", -180.25m),
                new CategoryBreakdownItem("Transport", -65.00m),
                new CategoryBreakdownItem("Utilities", -120.00m)
            });

        var json = JsonSerializer.Serialize(result, JsonSerializationOptions.Default);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("Period", root.GetProperty("type").GetString());
        Assert.Equal("2026-05-01..2026-05-31", root.GetProperty("period").GetString());
        Assert.Equal(1200.00m, root.GetProperty("incomeTotal").GetDecimal());
        Assert.Equal(430.50m, root.GetProperty("expenseTotal").GetDecimal());
        Assert.Equal(769.50m, root.GetProperty("netTotal").GetDecimal());

        var breakdown = root.GetProperty("categoryBreakdown");
        Assert.Equal(5, breakdown.GetArrayLength());

        Assert.Collection(
            breakdown.EnumerateArray(),
            item =>
            {
                Assert.Equal("Salary", item.GetProperty("category").GetString());
                Assert.Equal(1200.00m, item.GetProperty("total").GetDecimal());
            },
            item =>
            {
                Assert.Equal("Dining out", item.GetProperty("category").GetString());
                Assert.Equal(-65.25m, item.GetProperty("total").GetDecimal());
            },
            item =>
            {
                Assert.Equal("Groceries", item.GetProperty("category").GetString());
                Assert.Equal(-180.25m, item.GetProperty("total").GetDecimal());
            },
            item =>
            {
                Assert.Equal("Transport", item.GetProperty("category").GetString());
                Assert.Equal(-65.00m, item.GetProperty("total").GetDecimal());
            },
            item =>
            {
                Assert.Equal("Utilities", item.GetProperty("category").GetString());
                Assert.Equal(-120.00m, item.GetProperty("total").GetDecimal());
            });
    }

    [Fact]
    public void Empty_period_ReportResult_emits_empty_categoryBreakdown_not_null()
    {
        var empty = new ReportResult(
            Type: ReportType.Period,
            Period: "2026-05-01..2026-05-31",
            IncomeTotal: 0m,
            ExpenseTotal: 0m,
            NetTotal: 0m,
            CategoryBreakdown: Array.Empty<CategoryBreakdownItem>());

        var json = JsonSerializer.Serialize(empty, JsonSerializationOptions.Default);
        using var document = JsonDocument.Parse(json);
        var breakdown = document.RootElement.GetProperty("categoryBreakdown");

        Assert.Equal(JsonValueKind.Array, breakdown.ValueKind);
        Assert.Equal(0, breakdown.GetArrayLength());
    }
}
