using Finance.Data.Models;

namespace Finance.Business.Dtos.Categories;

public sealed record CategoryResponse(
    int Id,
    string Name,
    CategoryType Type);
