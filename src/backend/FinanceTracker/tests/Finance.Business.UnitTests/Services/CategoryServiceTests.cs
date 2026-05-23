using Finance.Business.Dtos.Categories;
using Finance.Business.Services;
using Finance.Data.Models;
using Finance.Data.Repositories;
using Moq;
using Xunit;

namespace Finance.Business.UnitTests.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repo = new(MockBehavior.Strict);

    private CategoryService NewService() => new(_repo.Object);

    [Fact]
    public void GetAll_maps_every_repository_record_to_response_dto()
    {
        _repo.Setup(r => r.GetAll()).Returns(new[]
        {
            new Category(1, "Salary",    CategoryType.Income),
            new Category(2, "Groceries", CategoryType.Expense),
        });

        var result = NewService().GetAll();

        Assert.Equal(2, result.Count);
        Assert.Collection(result,
            c => Assert.Equal((1, "Salary",    CategoryType.Income),  (c.Id, c.Name, c.Type)),
            c => Assert.Equal((2, "Groceries", CategoryType.Expense), (c.Id, c.Name, c.Type)));
    }

    [Fact]
    public void GetById_returns_mapped_response_when_repository_finds_record()
    {
        _repo.Setup(r => r.GetById(2))
            .Returns(new Category(2, "Groceries", CategoryType.Expense));

        var result = NewService().GetById(2);

        Assert.NotNull(result);
        Assert.Equal("Groceries", result!.Name);
    }

    [Fact]
    public void GetById_returns_null_when_repository_misses()
    {
        _repo.Setup(r => r.GetById(9999)).Returns((Category?)null);

        var result = NewService().GetById(9999);

        Assert.Null(result);
    }

    [Fact]
    public void Create_passes_entity_to_repository_and_maps_returned_record()
    {
        var stored = new Category(6, "Savings", CategoryType.Income);
        _repo.Setup(r => r.Add(It.Is<Category>(c => c.Name == "Savings" && c.Type == CategoryType.Income)))
             .Returns(stored);

        var result = NewService().Create(new CategoryCreateRequest("Savings", CategoryType.Income));

        Assert.Equal(6, result.Id);
        Assert.Equal("Savings", result.Name);
        _repo.Verify(r => r.Add(It.IsAny<Category>()), Times.Once);
    }

    [Fact]
    public void Create_propagates_InvalidOperationException_from_repository()
    {
        _repo.Setup(r => r.Add(It.IsAny<Category>()))
             .Throws(new InvalidOperationException("A category named 'GROCERIES' already exists."));

        var service = NewService();

        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Create(new CategoryCreateRequest("GROCERIES", CategoryType.Expense)));
        Assert.Contains("GROCERIES", ex.Message);
    }
}
