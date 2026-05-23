using Finance.Business.Enums;

namespace Finance.Business.Services.Reports;

public interface IReportStrategyFactory
{
    IReportStrategy? TryGet(ReportType type);
}
