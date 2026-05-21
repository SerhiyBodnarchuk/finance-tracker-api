using Finance.Data.Models;
using Finance.Data.Repositories;
using Xunit;

namespace Finance.Data.UnitTests.Repositories;

public class InMemoryCategoryRepositoryTests
{
    [Fact]
    public void GetAll_returns_five_seeded_categories_in_id_order()
    {
        var repo = new InMemoryCategoryRepository();

        var all = repo.GetAll().ToList();

        Assert.Equal(5, all.Count);
        Assert.Collection(all,
            c => { Assert.Equal(1, c.Id); Assert.Equal("Salary",        c.Name); Assert.Equal(CategoryType.Income,  c.Type); },
            c => { Assert.Equal(2, c.Id); Assert.Equal("Groceries",     c.Name); Assert.Equal(CategoryType.Expense, c.Type); },
            c => { Assert.Equal(3, c.Id); Assert.Equal("Transport",     c.Name); Assert.Equal(CategoryType.Expense, c.Type); },
            c => { Assert.Equal(4, c.Id); Assert.Equal("Entertainment", c.Name); Assert.Equal(CategoryType.Expense, c.Type); },
            c => { Assert.Equal(5, c.Id); Assert.Equal("Utilities",     c.Name); Assert.Equal(CategoryType.Expense, c.Type); });
    }

    [Fact]
    public void GetById_returns_seeded_category_by_literal_id()
    {
        var repo = new InMemoryCategoryRepository();

        var salary = repo.GetById(1);
        var utilities = repo.GetById(5);

        Assert.NotNull(salary);
        Assert.Equal("Salary", salary!.Name);
        Assert.NotNull(utilities);
        Assert.Equal(CategoryType.Expense, utilities!.Type);
    }

    [Fact]
    public void Add_appends_new_category_and_assigns_next_id()
    {
        var repo = new InMemoryCategoryRepository();

        var stored = repo.Add(new Category(0, "Savings", CategoryType.Income));

        Assert.Equal(6, stored.Id);
        Assert.Equal("Savings", stored.Name);
        Assert.Equal(CategoryType.Income, stored.Type);

        var all = repo.GetAll().ToList();
        Assert.Equal(6, all.Count);
        Assert.Same(stored, all[^1]);
    }

    [Fact]
    public void Add_rejects_duplicate_name_case_insensitively_without_consuming_id()
    {
        var repo = new InMemoryCategoryRepository();

        var ex = Assert.Throws<InvalidOperationException>(
            () => repo.Add(new Category(0, "GROCERIES", CategoryType.Expense)));
        Assert.Contains("GROCERIES", ex.Message);

        var stored = repo.Add(new Category(0, "Brand-new", CategoryType.Expense));
        Assert.Equal(6, stored.Id);
    }

    [Fact]
    public void GetById_returns_null_for_unknown_id()
    {
        var repo = new InMemoryCategoryRepository();

        Assert.Null(repo.GetById(99999));
        Assert.Null(repo.GetById(0));
    }
}
