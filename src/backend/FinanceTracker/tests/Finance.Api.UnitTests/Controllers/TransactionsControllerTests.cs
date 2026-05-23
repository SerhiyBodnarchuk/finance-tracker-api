using Finance.Api.Controllers;
using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Finance.Business.Validation;
using Finance.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Finance.Api.UnitTests.Controllers;

public class TransactionsControllerTests
{
    private readonly Mock<ITransactionService> _service = new(MockBehavior.Strict);
    private readonly Mock<ITransactionValidator> _validator = new(MockBehavior.Strict);

    private TransactionsController NewController() => new(_service.Object, _validator.Object);

    private static readonly TransactionResponse Sample = new(
        Id: 3,
        Timestamp: new DateTime(2026, 5, 5, 12, 0, 0),
        Description: "Silpo Market",
        Amount: 32.10m,
        Type: TransactionType.Expense,
        Categories: new[] { new CategorySummary(2, "Groceries") });

    private static readonly TransactionCreateRequest ValidCreate = new(
        Timestamp: new DateTime(2026, 5, 31, 12, 0, 0),
        Description: "Coffee",
        Amount: 4.50m,
        Type: TransactionType.Expense,
        CategoryIds: new[] { 2 });

    [Fact]
    public void List_returns_200_with_service_collection()
    {
        IReadOnlyCollection<TransactionResponse> stored = new[] { Sample };
        _service.Setup(s => s.GetAll()).Returns(stored);

        var result = NewController().List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(stored, ok.Value);
    }

    [Fact]
    public void GetById_returns_200_when_service_has_record()
    {
        _service.Setup(s => s.GetById(3)).Returns(Sample);

        var result = NewController().GetById(3);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(Sample, ok.Value);
    }

    [Fact]
    public void GetById_returns_404_problem_when_service_returns_null()
    {
        _service.Setup(s => s.GetById(9999)).Returns((TransactionResponse?)null);

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
            .Returns(ValidationResult.Failure(new ValidationError("amount", "Amount must be greater than zero.")));

        var result = NewController().Create(ValidCreate);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.True(problem.Errors.ContainsKey("amount"));
        _service.Verify(s => s.Create(It.IsAny<TransactionCreateRequest>()), Times.Never);
    }

    [Fact]
    public void Create_returns_201_with_location_when_validator_and_service_succeed()
    {
        _validator.Setup(v => v.ValidateForCreate(ValidCreate)).Returns(ValidationResult.Success);
        var stored = Sample with { Id = 6, Description = "Coffee", Amount = 4.50m };
        _service.Setup(s => s.Create(ValidCreate)).Returns(stored);

        var result = NewController().Create(ValidCreate);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TransactionsController.GetById), created.ActionName);
        Assert.Equal(6, created.RouteValues!["id"]);
        Assert.Same(stored, created.Value);
    }

    [Fact]
    public void Delete_returns_204_when_service_removes_record()
    {
        _service.Setup(s => s.Delete(4)).Returns(true);

        var result = NewController().Delete(4);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void Delete_returns_404_problem_when_service_reports_miss()
    {
        _service.Setup(s => s.Delete(9999)).Returns(false);

        var result = NewController().Delete(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }
}
