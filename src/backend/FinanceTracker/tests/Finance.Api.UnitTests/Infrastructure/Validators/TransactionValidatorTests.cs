using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Categories;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Finance.Data.Models;
using Moq;
using Xunit;

namespace Finance.Api.UnitTests.Infrastructure.Validators;

public class TransactionValidatorTests
{
    private readonly Mock<ICategoryService> _categories = new(MockBehavior.Strict);

    private TransactionValidator NewValidator() => new(_categories.Object);

    private static readonly CategoryResponse SalaryCategory    = new(1, "Salary",    CategoryType.Income);
    private static readonly CategoryResponse GroceriesCategory = new(2, "Groceries", CategoryType.Expense);
    private static readonly CategoryResponse MiscCategory      = new(6, "Misc",      CategoryType.Both);

    private static TransactionCreateRequest CreateRequest(
        decimal amount = 1m,
        string description = "Coffee",
        TransactionType type = TransactionType.Expense,
        IReadOnlyList<int>? categoryIds = null) =>
        new(
            Timestamp: new DateTime(2026, 5, 31, 12, 0, 0),
            Description: description,
            Amount: amount,
            Type: type,
            CategoryIds: categoryIds ?? new[] { 2 });

    [Fact]
    public void Returns_success_for_valid_request()
    {
        _categories.Setup(s => s.GetById(2)).Returns(GroceriesCategory);

        var result = NewValidator().ValidateForCreate(CreateRequest(amount: 4.50m));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Returns_failure_when_amount_is_zero()
    {
        _categories.Setup(s => s.GetById(2)).Returns(GroceriesCategory);

        var result = NewValidator().ValidateForCreate(CreateRequest(amount: 0m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "amount");
    }

    [Fact]
    public void Returns_failure_when_amount_is_negative()
    {
        _categories.Setup(s => s.GetById(2)).Returns(GroceriesCategory);

        var result = NewValidator().ValidateForCreate(CreateRequest(amount: -1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "amount");
    }

    [Fact]
    public void Returns_failure_when_description_is_whitespace()
    {
        _categories.Setup(s => s.GetById(2)).Returns(GroceriesCategory);

        var result = NewValidator().ValidateForCreate(CreateRequest(description: "   "));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "description");
    }

    [Fact]
    public void Returns_failure_when_categoryIds_is_empty_without_calling_service()
    {
        var result = NewValidator().ValidateForCreate(CreateRequest(categoryIds: Array.Empty<int>()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "categoryIds");
        _categories.Verify(s => s.GetById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Returns_failure_with_index_when_categoryId_does_not_exist()
    {
        _categories.Setup(s => s.GetById(9999)).Returns((CategoryResponse?)null);

        var result = NewValidator().ValidateForCreate(CreateRequest(categoryIds: new[] { 9999 }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "categoryIds[0]" && e.Message.Contains("9999"));
    }

    [Fact]
    public void Returns_failure_when_expense_transaction_references_income_only_category()
    {
        _categories.Setup(s => s.GetById(1)).Returns(SalaryCategory);

        var result = NewValidator().ValidateForCreate(
            CreateRequest(type: TransactionType.Expense, categoryIds: new[] { 1 }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field.StartsWith("categoryIds[", StringComparison.Ordinal));
    }

    [Fact]
    public void Allows_expense_transaction_with_Both_type_category()
    {
        _categories.Setup(s => s.GetById(2)).Returns(GroceriesCategory);
        _categories.Setup(s => s.GetById(6)).Returns(MiscCategory);

        var result = NewValidator().ValidateForCreate(
            CreateRequest(type: TransactionType.Expense, categoryIds: new[] { 2, 6 }));

        Assert.True(result.IsValid);
    }
}
