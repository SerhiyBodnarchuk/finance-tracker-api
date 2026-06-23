using Finance.Api.Infrastructure;
using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Export;
using Finance.Business.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController(
    ITransactionService transactions,
    ITransactionValidator validator) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TransactionResponse>> List() =>
        Ok(transactions.GetAll());

    [HttpGet("{id:int}")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<TransactionResponse> GetById(int id)
    {
        var response = transactions.GetById(id);
        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Transaction not found",
                Detail = $"No transaction with id {id}."
            });
        }
        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<TransactionResponse> Create([FromBody] TransactionCreateRequest request)
    {
        var result = validator.ValidateForCreate(request);
        if (!result.IsValid)
            return BadRequest(ProblemDetailsMappers.ToValidationProblem(result));

        var stored = transactions.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored);
    }

    [HttpGet("export")]
    public IActionResult Export()
    {
        MediaTypeHeaderValue.TryParseList(Request.Headers.Accept, out var accepts);
        var sorted = (accepts ?? [])
            .OrderByDescending(m => m.Quality ?? 1.0)
            .ToList();

        if (!sorted.Any())
            return ServeJson();

        foreach (var media in sorted)
        {
            var type = media.MediaType.Value;
            if (type is "application/json" or "*/*")
                return ServeJson();
            if (type == "text/csv")
                return ServeCsv();
        }

        return StatusCode(StatusCodes.Status406NotAcceptable);

        IActionResult ServeJson()
        {
            Response.Headers.Append("Content-Disposition", "attachment; filename=transactions.json");
            return Ok(transactions.GetAll());
        }

        IActionResult ServeCsv()
        {
            var csv = TransactionCsvFormatter.Format(transactions.GetAll());
            Response.Headers.Append("Content-Disposition", "attachment; filename=transactions.csv");
            return Content(csv, "text/csv; charset=utf-8");
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        if (transactions.Delete(id))
            return NoContent();

        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Transaction not found",
            Detail = $"No transaction with id {id}."
        });
    }
}
