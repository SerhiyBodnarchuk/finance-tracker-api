using System.Text.Json;
using Finance.Api.Controllers;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Finance.Business.Services;
using Finance.Business.Services.Reports;
using Finance.Business.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Finance.Api.UnitTests.Controllers;

public class ReportsControllerTests
{
    private readonly Mock<IReportService> _service = new(MockBehavior.Strict);

    private ReportsController NewController() => new(_service.Object);

    private static ReportRequest PeriodRequest() =>
        new(ReportType.Period, JsonDocument.Parse("""{"start":"2026-05-01","end":"2026-05-31"}""").RootElement);

    [Fact]
    public void Generate_returns_200_with_service_result_when_service_resolves_strategy()
    {
        var request = PeriodRequest();
        var expected = new ReportResult(
            ReportType.Period, "2026-05-01..2026-05-31",
            1200m, 121.29m, 1078.71m, Array.Empty<CategoryBreakdownItem>());
        _service.Setup(s => s.TryGenerate(request)).Returns(expected);

        var result = NewController().Generate(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public void Generate_returns_400_unsupported_problem_when_service_returns_null()
    {
        var request = new ReportRequest(ReportType.IsoWeek, JsonDocument.Parse("{}").RootElement);
        _service.Setup(s => s.TryGenerate(request)).Returns((ReportResult?)null);

        var result = NewController().Generate(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Unsupported report type", problem.Title);
        Assert.Contains("IsoWeek", problem.Detail);
    }

    [Fact]
    public void Generate_returns_400_validation_problem_when_service_throws_ReportValidationException()
    {
        var request = PeriodRequest();
        _service.Setup(s => s.TryGenerate(request))
            .Throws(new ReportValidationException(new[]
            {
                new ValidationError("data", "Start date must be less than or equal to end date.")
            }));

        var result = NewController().Generate(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.True(problem.Errors.ContainsKey("data"));
    }
}
