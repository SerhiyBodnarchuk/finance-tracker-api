using Finance.Business.Dtos.Reports;

namespace Finance.Business.Services;

public interface IReportService
{
    /// <summary>
    /// Returns the generated report, or <c>null</c> if no strategy is registered
    /// for the request's <c>Type</c>. Throws <see cref="Reports.ReportValidationException"/>
    /// when the resolved strategy rejects the <c>Data</c> payload.
    /// </summary>
    ReportResult? TryGenerate(ReportRequest request);
}
