using Finance.Data.Models;

namespace Finance.Business.Dtos.Categories;

public sealed record CategoryCreateRequest(
    string Name,
    CategoryType Type);
