namespace Finance.Data.Models;

public sealed record Category(
    int Id,
    string Name,
    CategoryType Type);
