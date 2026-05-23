using Finance.Business.Dtos.Categories;
using Finance.Business.Mappers;
using Finance.Data.Repositories;

namespace Finance.Business.Services;

public sealed class CategoryService(ICategoryRepository categories) : ICategoryService
{
    public IReadOnlyCollection<CategoryResponse> GetAll() =>
        categories.GetAll()
            .Select(c => c.ToResponse())
            .ToList()
            .AsReadOnly();

    public CategoryResponse? GetById(int id) =>
        categories.GetById(id)?.ToResponse();

    public CategoryResponse Create(CategoryCreateRequest request)
    {
        var stored = categories.Add(request.ToEntity(assignedId: 0));
        return stored.ToResponse();
    }
}
