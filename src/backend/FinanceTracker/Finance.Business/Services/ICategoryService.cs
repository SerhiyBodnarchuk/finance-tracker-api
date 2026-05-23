using Finance.Business.Dtos.Categories;

namespace Finance.Business.Services;

public interface ICategoryService
{
    IReadOnlyCollection<CategoryResponse> GetAll();
    CategoryResponse? GetById(int id);
    CategoryResponse Create(CategoryCreateRequest request);
}