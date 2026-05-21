using Finance.Data.Models;

namespace Finance.Data.Repositories;

public sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly List<Category> _categories;
    private int _nextId;

    public InMemoryCategoryRepository()
    {
        _categories = new List<Category>
        {
            new(1, "Salary", CategoryType.Income),
            new(2, "Groceries", CategoryType.Expense),
            new(3, "Transport", CategoryType.Expense),
            new(4, "Entertainment", CategoryType.Expense),
            new(5, "Utilities", CategoryType.Expense)
        };
        _nextId = 6;
    }

    public IReadOnlyCollection<Category> GetAll()
    {
        return _categories.AsReadOnly();
    }

    public Category? GetById(int id)
    {
        return _categories.FirstOrDefault(c => c.Id == id);
    }

    public Category Add(Category category)
    {
        if (_categories.Any(c => string.Equals(c.Name, category.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A category named '{category.Name}' already exists.");

        var stored = category with { Id = _nextId };
        _categories.Add(stored);
        _nextId++;
        return stored;
    }
}