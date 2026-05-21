namespace Finance.Business.Dtos.Reports;

public sealed record PeriodReportData(
    DateOnly Start,
    DateOnly End);
