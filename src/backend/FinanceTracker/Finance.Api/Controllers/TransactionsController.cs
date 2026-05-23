using Finance.Api.Infrastructure;
using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Microsoft.AspNetCore.Mvc;

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
    public ActionResult<TransactionResponse> Create([FromBody] TransactionCreateRequest request)
    {
        var result = validator.ValidateForCreate(request);
        if (!result.IsValid)
            return BadRequest(ProblemDetailsMappers.ToValidationProblem(result));

        var stored = transactions.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored);
    }

    [HttpDelete("{id:int}")]
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
