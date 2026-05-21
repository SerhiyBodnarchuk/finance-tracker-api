using Finance.Data.Models;

namespace Finance.Data.Repositories;

public interface ICategoryRepository
{
    IReadOnlyCollection<Category> GetAll();
    Category? GetById(int id);
    Category Add(Category category);
}
