using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;

namespace Finance.Business.Services.Reports;

public interface IReportStrategy
{
    ReportType Type { get; }

    ReportResult Generate(ReportRequest request);
}
