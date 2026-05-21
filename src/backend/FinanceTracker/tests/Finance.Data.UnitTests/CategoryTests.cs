using Finance.Data.Models;
using Xunit;

namespace Finance.Data.UnitTests;

public class CategoryTests
{
    [Theory]
    [InlineData(CategoryType.Income)]
    [InlineData(CategoryType.Expense)]
    [InlineData(CategoryType.Both)]
    public void Construct_with_each_CategoryType_exposes_fields_as_supplied(CategoryType type)
    {
        var category = new Category(Id: 1, Name: "Groceries", Type: type);

        Assert.Equal(1, category.Id);
        Assert.Equal("Groceries", category.Name);
        Assert.Equal(type, category.Type);
    }

    [Fact]
    public void Names_differing_only_by_case_compare_equal_under_OrdinalIgnoreCase()
    {
        var groceries = new Category(1, "Groceries", CategoryType.Expense);
        var lowercase = new Category(2, "groceries", CategoryType.Expense);

        Assert.True(
            StringComparer.OrdinalIgnoreCase.Equals(groceries.Name, lowercase.Name));
    }
}
