namespace Finance.Business.Dtos.Reports;

public sealed record CategoryBreakdownItem(
    string Category,
    decimal Total);
