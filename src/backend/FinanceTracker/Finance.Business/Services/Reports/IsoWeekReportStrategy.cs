using System.Globalization;
using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Validation;
using Finance.Data.Models;
using Finance.Data.Repositories;

namespace Finance.Business.Services.Reports;

public sealed class IsoWeekReportStrategy(
    ITransactionRepository transactions,
    ICategoryRepository categories)
    : IReportStrategy
{
    public ReportType Type => ReportType.IsoWeek;

    public ReportResult Generate(ReportRequest request)
    {
        var data = ParseAndValidate(request);

        var (rangeStart, rangeEnd, period) = ResolveRange(data);

        var inWindow = new List<Transaction>();
        foreach (var t in transactions.GetAll())
        {
            if (t.Timestamp >= rangeStart && t.Timestamp <= rangeEnd)
            {
                inWindow.Add(t);
            }
        }

        var incomeTotal = 0m;
        var expenseTotal = 0m;
        foreach (var t in inWindow)
        {
            if (t.Type == TransactionType.Income)
            {
                incomeTotal += t.Amount;
            }
            else
            {
                expenseTotal += t.Amount;
            }
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

        return new ReportResult(
            ReportType.IsoWeek,
            period,
            incomeTotal,
            expenseTotal,
            netTotal,
            breakdown);
    }

    private static (DateTime Start, DateTime End, string Period) ResolveRange(IsoWeekReportData data)
    {
        // Format already validated by ParseAndValidate, so the parses below cannot fail.
        var year = int.Parse(data.Week.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var week = int.Parse(data.Week.AsSpan(6, 2), CultureInfo.InvariantCulture);

        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        var start = monday.Date;
        var end = monday.AddDays(6).Date + new TimeSpan(23, 59, 59);
        var period = $"{year:D4}-W{week:D2}";

        return (start, end, period);
    }

    private static IsoWeekReportData ParseAndValidate(ReportRequest request)
    {
        IsoWeekReportData? data;
        try
        {
            data = request.Data.Deserialize<IsoWeekReportData>(JsonSerializationOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", $"Invalid iso week report data payload: {ex.Message}")
            });
        }

        if (data is null)
            throw new ReportValidationException(new[]
            {
                new ValidationError("data", "IsoWeek report data payload is required.")
            });

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(data.Week))
            errors.Add(new ValidationError("data.week", "Week is required."));
        else if (!IsValidIsoWeek(data.Week))
            errors.Add(new ValidationError("data.week",
                $"Week '{data.Week}' is not a valid ISO 8601 week (expected 'yyyy-Www')."));

        if (errors.Count > 0)
            throw new ReportValidationException(errors);

        return data;
    }

    private static bool IsValidIsoWeek(string value)
    {
        if (value.Length != 8 || value[4] != '-' || value[5] != 'W')
            return false;
        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            return false;
        if (!int.TryParse(value.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var week))
            return false;
        // ISOWeek.ToDateTime silently spills over for out-of-range weeks (53 in a 52-week year returns
        // the next year's W1), so bound the week against the year's actual ISO-week count explicitly.
        return week >= 1 && week <= ISOWeek.GetWeeksInYear(year);
    }
}