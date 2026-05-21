using Finance.Business.Enums;

namespace Finance.Business.Dtos.Reports;

public sealed record ReportResult(
    ReportType Type,
    string Period,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal NetTotal,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown);
