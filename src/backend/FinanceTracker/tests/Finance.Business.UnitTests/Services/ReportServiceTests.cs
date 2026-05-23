using System.Text.Json;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Services;
using Finance.Business.Services.Reports;
using Finance.Business.Validation;
using Moq;
using Xunit;

namespace Finance.Business.UnitTests.Services;

public class ReportServiceTests
{
    private readonly Mock<IReportStrategyFactory> _factory = new(MockBehavior.Strict);

    private ReportService NewService() => new(_factory.Object);

    private static ReportRequest AnyRequest(ReportType type) =>
        new(type, JsonDocument.Parse("{}").RootElement);

    [Fact]
    public void TryGenerate_returns_result_when_factory_resolves_strategy()
    {
        var request = AnyRequest(ReportType.Period);
        var expected = new ReportResult(
            ReportType.Period, "2026-05-01..2026-05-31",
            1m, 0m, 1m, Array.Empty<CategoryBreakdownItem>());
        var strategy = new Mock<IReportStrategy>(MockBehavior.Strict);
        strategy.Setup(s => s.Generate(request)).Returns(expected);
        _factory.Setup(f => f.TryGet(ReportType.Period)).Returns(strategy.Object);

        var result = NewService().TryGenerate(request);

        Assert.Same(expected, result);
        strategy.Verify(s => s.Generate(request), Times.Once);
    }

    [Fact]
    public void TryGenerate_returns_null_when_factory_does_not_resolve_strategy()
    {
        var request = AnyRequest(ReportType.IsoWeek);
        _factory.Setup(f => f.TryGet(ReportType.IsoWeek)).Returns((IReportStrategy?)null);

        var result = NewService().TryGenerate(request);

        Assert.Null(result);
    }

    [Fact]
    public void TryGenerate_propagates_ReportValidationException_from_strategy()
    {
        var request = AnyRequest(ReportType.Period);
        var strategy = new Mock<IReportStrategy>(MockBehavior.Strict);
        strategy.Setup(s => s.Generate(request))
            .Throws(new ReportValidationException(new[] { new ValidationError("data", "boom") }));
        _factory.Setup(f => f.TryGet(ReportType.Period)).Returns(strategy.Object);

        var service = NewService();

        var ex = Assert.Throws<ReportValidationException>(() => service.TryGenerate(request));
        Assert.Single(ex.Errors);
        Assert.Equal("data", ex.Errors[0].Field);
    }
}
