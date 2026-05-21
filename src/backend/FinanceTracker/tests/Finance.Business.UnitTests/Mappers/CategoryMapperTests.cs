using Finance.Business.Dtos.Categories;
using Finance.Business.Mappers;
using Finance.Data.Models;
using Xunit;

namespace Finance.Business.UnitTests.Mappers;

public class CategoryMapperTests
{
    [Fact]
    public void ToResponse_copies_the_three_fields()
    {
        var category = new Category(7, "Groceries", CategoryType.Expense);

        var response = category.ToResponse();

        Assert.Equal(7, response.Id);
        Assert.Equal("Groceries", response.Name);
        Assert.Equal(CategoryType.Expense, response.Type);
    }

    [Fact]
    public void ToEntity_carries_assigned_id_and_preserves_other_fields()
    {
        var request = new CategoryCreateRequest("Groceries", CategoryType.Expense);

        var entity = request.ToEntity(assignedId: 42);

        Assert.Equal(42, entity.Id);
        Assert.Equal("Groceries", entity.Name);
        Assert.Equal(CategoryType.Expense, entity.Type);
    }
}
