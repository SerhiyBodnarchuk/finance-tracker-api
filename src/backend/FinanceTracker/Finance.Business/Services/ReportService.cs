using Finance.Business.Dtos.Reports;
using Finance.Business.Services.Reports;

namespace Finance.Business.Services;

public sealed class ReportService(IReportStrategyFactory factory) : IReportService
{
    public ReportResult? TryGenerate(ReportRequest request)
    {
        var strategy = factory.TryGet(request.Type);
        return strategy?.Generate(request);
    }
}
