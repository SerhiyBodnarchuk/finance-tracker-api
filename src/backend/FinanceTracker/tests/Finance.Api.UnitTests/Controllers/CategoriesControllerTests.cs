using Finance.Api.Controllers;
using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Business.Validation;
using Finance.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Finance.Api.UnitTests.Controllers;

public class CategoriesControllerTests
{
    private readonly Mock<ICategoryService> _service = new(MockBehavior.Strict);
    private readonly Mock<ICategoryValidator> _validator = new(MockBehavior.Strict);

    private CategoriesController NewController() => new(_service.Object, _validator.Object);

    private static readonly CategoryResponse Sample = new(2, "Groceries", CategoryType.Expense);

    private static readonly CategoryCreateRequest ValidCreate = new("Savings", CategoryType.Income);

    [Fact]
    public void List_returns_200_with_service_collection()
    {
        IReadOnlyCollection<CategoryResponse> stored = new[] { Sample };
        _service.Setup(s => s.GetAll()).Returns(stored);

        var result = NewController().List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(stored, ok.Value);
    }

    [Fact]
    public void GetById_returns_200_when_service_has_record()
    {
        _service.Setup(s => s.GetById(2)).Returns(Sample);

        var result = NewController().GetById(2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(Sample, ok.Value);
    }

    [Fact]
    public void GetById_returns_404_problem_when_service_returns_null()
    {
        _service.Setup(s => s.GetById(9999)).Returns((CategoryResponse?)null);

        var result = NewController().GetById(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Contains("9999", problem.Detail);
    }

    [Fact]
    public void Create_returns_400_when_validator_fails_without_calling_service()
    {
        _validator.Setup(v => v.ValidateForCreate(ValidCreate))
            .Returns(ValidationResult.Failure(new ValidationError("name", "Name is required.")));

        var result = NewController().Create(ValidCreate);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.True(problem.Errors.ContainsKey("name"));
        _service.Verify(s => s.Create(It.IsAny<CategoryCreateRequest>()), Times.Never);
    }

    [Fact]
    public void Create_returns_201_with_location_when_validator_and_service_succeed()
    {
        _validator.Setup(v => v.ValidateForCreate(ValidCreate)).Returns(ValidationResult.Success);
        var stored = new CategoryResponse(6, "Savings", CategoryType.Income);
        _service.Setup(s => s.Create(ValidCreate)).Returns(stored);

        var result = NewController().Create(ValidCreate);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CategoriesController.GetById), created.ActionName);
        Assert.Equal(6, created.RouteValues!["id"]);
        Assert.Same(stored, created.Value);
    }

    [Fact]
    public void Create_returns_409_problem_when_service_throws_InvalidOperationException()
    {
        _validator.Setup(v => v.ValidateForCreate(ValidCreate)).Returns(ValidationResult.Success);
        _service.Setup(s => s.Create(ValidCreate))
            .Throws(new InvalidOperationException("A category named 'Savings' already exists."));

        var result = NewController().Create(ValidCreate);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Contains("Savings", problem.Detail);
    }
}
