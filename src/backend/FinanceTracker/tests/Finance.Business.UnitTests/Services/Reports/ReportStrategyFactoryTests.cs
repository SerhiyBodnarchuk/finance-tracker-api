using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Services.Reports;
using Finance.Data.Repositories;
using Moq;
using Xunit;

namespace Finance.Business.UnitTests.Services.Reports;

public class ReportStrategyFactoryTests
{
    private sealed class StubStrategy(ReportType type) : IReportStrategy
    {
        public ReportType Type { get; } = type;
        public ReportResult Generate(ReportRequest request) => throw new NotImplementedException();
    }

    [Fact]
    public void TryGet_returns_strategy_for_registered_Period()
    {
        var period = new StubStrategy(ReportType.Period);
        var factory = new ReportStrategyFactory(new[] { (IReportStrategy)period });

        var resolved = factory.TryGet(ReportType.Period);

        Assert.Same(period, resolved);
    }

    [Fact]
    public void TryGet_returns_null_for_IsoWeek_when_no_strategy_registered()
    {
        var factory = new ReportStrategyFactory(new[] { (IReportStrategy)new StubStrategy(ReportType.Period) });

        var resolved = factory.TryGet(ReportType.IsoWeek);

        Assert.Null(resolved);
    }

    [Fact]
    public void Constructor_throws_when_two_strategies_share_a_type()
    {
        var a = new StubStrategy(ReportType.Period);
        var b = new StubStrategy(ReportType.Period);

        Assert.Throws<ArgumentException>(() => new ReportStrategyFactory(new IReportStrategy[] { a, b }));
    }

    [Fact]
    public void Factory_resolves_IsoWeek_to_IsoWeekReportStrategy()
    {
        var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);
        var categories = new Mock<ICategoryRepository>(MockBehavior.Strict);
        var isoWeek = new IsoWeekReportStrategy(transactions.Object, categories.Object);
        var factory = new ReportStrategyFactory(new IReportStrategy[] { isoWeek });

        var resolved = factory.TryGet(ReportType.IsoWeek);

        Assert.NotNull(resolved);
        Assert.Equal(ReportType.IsoWeek, resolved!.Type);
        Assert.IsType<IsoWeekReportStrategy>(resolved);
    }
}
