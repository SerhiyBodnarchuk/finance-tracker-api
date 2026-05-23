using Finance.Api.Infrastructure.Validators;
using Finance.Business.Dtos.Categories;
using Finance.Data.Models;
using Xunit;

namespace Finance.Api.UnitTests.Infrastructure.Validators;

public class CategoryValidatorTests
{
    private static readonly CategoryValidator Validator = new();

    [Fact]
    public void Returns_success_for_valid_request()
    {
        var result = Validator.ValidateForCreate(new CategoryCreateRequest("Savings", CategoryType.Income));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Returns_failure_when_name_is_empty()
    {
        var result = Validator.ValidateForCreate(new CategoryCreateRequest("", CategoryType.Expense));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "name");
    }

    [Fact]
    public void Returns_failure_when_name_is_whitespace_only()
    {
        var result = Validator.ValidateForCreate(new CategoryCreateRequest("   ", CategoryType.Expense));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "name");
    }
}
