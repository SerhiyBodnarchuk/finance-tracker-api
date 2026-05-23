using Finance.Business.Enums;

namespace Finance.Business.Services.Reports;

public sealed class ReportStrategyFactory : IReportStrategyFactory
{
    private readonly IReadOnlyDictionary<ReportType, IReportStrategy> _strategies;

    public ReportStrategyFactory(IEnumerable<IReportStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Type);
    }

    public IReportStrategy? TryGet(ReportType type) =>
        _strategies.TryGetValue(type, out var strategy) ? strategy : null;
}
