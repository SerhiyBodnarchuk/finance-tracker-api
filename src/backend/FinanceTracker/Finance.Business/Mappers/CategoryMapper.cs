using Finance.Business.Dtos.Categories;
using Finance.Data.Models;

namespace Finance.Business.Mappers;

public static class CategoryMapper
{
    public static CategoryResponse ToResponse(this Category category) =>
        new(category.Id, category.Name, category.Type);

    public static Category ToEntity(this CategoryCreateRequest request, int assignedId) =>
        new(assignedId, request.Name, request.Type);
}
