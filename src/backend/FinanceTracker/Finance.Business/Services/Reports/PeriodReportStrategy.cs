using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Validation;
using Finance.Data.Models;
using Finance.Data.Repositories;

namespace Finance.Business.Services.Reports;

public sealed class PeriodReportStrategy(
    ITransactionRepository transactions,
    ICategoryRepository categories)
    : IReportStrategy
{
    public ReportType Type => ReportType.Period;

    public ReportResult Generate(ReportRequest request)
    {
        var data = ParseAndValidate(request);

        var rangeStart = data.Start.ToDateTime(TimeOnly.MinValue);
        var rangeEnd   = data.End.ToDateTime(new TimeOnly(23, 59, 59));

        var inWindow = new List<Transaction>();
        foreach (var t in transactions.GetAll())
        {
            if (t.Timestamp >= rangeStart && t.Timestamp <= rangeEnd)
                inWindow.Add(t);
        }

        var incomeTotal = 0m;
        var expenseTotal = 0m;
        foreach (var t in inWindow)
        {
            if (t.Type == TransactionType.Income) incomeTotal  += t.Amount;
            else                                  expenseTotal += t.Amount;
        }
        var netTotal = incomeTotal - expenseTotal;

        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var breakdownTotals = new Dictionary<int, decimal>();
        foreach (var t in inWindow)
        {
            var signed = t.Type == TransactionType.Income ? t.Amount : -t.Amount;
            foreach (var categoryId in t.CategoryIds)
            {
                breakdownTotals[categoryId] = breakdownTotals.TryGetValue(categoryId, out var current)
                    ? current + signed
                    : signed;
            }
        }

        var breakdown = breakdownTotals
            .Select(kvp => new CategoryBreakdownItem(categoryNames[kvp.Key], kvp.Value))
            .OrderBy(item => item.Total < 0m)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToList();

        var period = $"{data.Start:yyyy-MM-dd}..{data.End:yyyy-MM-dd}";

        return new ReportResult(
            ReportType.Period,
            period,
            incomeTotal,
            expenseTotal,
            netTotal,
            breakdown);
    }

    private static PeriodReportData ParseAndValidate(ReportRequest request)
    {
        PeriodReportData? data;
        try
        {
            data = request.Data.Deserialize<PeriodReportData>(JsonSerializationOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", $"Invalid period report data payload: {ex.Message}")
            });
        }

        if (data is null)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", "Period report data payload is required.")
            });
        }

        var errors = new List<ValidationError>();
        if (data.Start == default)
            errors.Add(new ValidationError("data.start", "Start date is required."));
        if (data.End == default)
            errors.Add(new ValidationError("data.end", "End date is required."));
        if (errors.Count == 0 && data.Start > data.End)
            errors.Add(new ValidationError("data", "Start date must be less than or equal to end date."));

        if (errors.Count > 0)
            throw new ReportValidationException(errors);

        return data;
    }
}
