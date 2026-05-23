using Finance.Api.Infrastructure;
using Finance.Business.Dtos.Reports;
using Finance.Business.Services;
using Finance.Business.Services.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpPost]
    public ActionResult<ReportResult> Generate([FromBody] ReportRequest request)
    {
        try
        {
            var result = reports.TryGenerate(request);
            if (result is null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Unsupported report type",
                    Detail = $"Report type '{request.Type}' is not supported."
                });
            }
            return Ok(result);
        }
        catch (ReportValidationException ex)
        {
            return BadRequest(ProblemDetailsMappers.ToValidationProblem(ex.Errors));
        }
    }
}
