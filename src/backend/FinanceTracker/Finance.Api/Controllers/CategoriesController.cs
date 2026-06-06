using Finance.Api.Infrastructure;
using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController(
    ICategoryService categories,
    ICategoryValidator validator) : ControllerBase
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<CategoryResponse>> List() =>
        Ok(categories.GetAll());

    [HttpGet("{id:int}")]
    public ActionResult<CategoryResponse> GetById(int id)
    {
        var response = categories.GetById(id);
        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Category not found",
                Detail = $"No category with id {id}."
            });
        }
        return Ok(response);
    }

    [HttpPost]
    public ActionResult<CategoryResponse> Create([FromBody] CategoryCreateRequest request)
    {
        var result = validator.ValidateForCreate(request);
        if (!result.IsValid)
            return BadRequest(ProblemDetailsMappers.ToValidationProblem(result));

        try
        {
            var stored = categories.Create(request);
            return CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate category name",
                Detail = ex.Message
            });
        }
    }
}
