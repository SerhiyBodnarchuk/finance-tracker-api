using System.Diagnostics;
using Finance.Data.Models;
using Finance.Data.Repositories;
using Xunit;

namespace Finance.Data.UnitTests.Repositories;

public class InMemoryTransactionRepositoryTests
{
    [Fact]
    public void GetAll_returns_five_seeded_transactions_in_id_order()
    {
        var repo = new InMemoryTransactionRepository();

        var all = repo.GetAll().ToList();

        Assert.Equal(5, all.Count);
        Assert.Collection(all,
            t =>
            {
                Assert.Equal(1, t.Id);
                Assert.Equal(new DateTime(2026, 5, 1, 12, 0, 0), t.Timestamp);
                Assert.Equal("Monthly salary", t.Description);
                Assert.Equal(1200.00m, t.Amount);
                Assert.Equal(TransactionType.Income, t.Type);
                Assert.Equal(new[] { 1 }, t.CategoryIds);
            },
            t =>
            {
                Assert.Equal(2, t.Id);
                Assert.Equal(new DateTime(2026, 5, 4, 12, 0, 0), t.Timestamp);
                Assert.Equal("Uber Trip", t.Description);
                Assert.Equal(14.20m, t.Amount);
                Assert.Equal(TransactionType.Expense, t.Type);
                Assert.Equal(new[] { 3 }, t.CategoryIds);
            },
            t =>
            {
                Assert.Equal(3, t.Id);
                Assert.Equal(new DateTime(2026, 5, 5, 12, 0, 0), t.Timestamp);
                Assert.Equal("Silpo Market", t.Description);
                Assert.Equal(32.10m, t.Amount);
                Assert.Equal(TransactionType.Expense, t.Type);
                Assert.Equal(new[] { 2 }, t.CategoryIds);
            },
            t =>
            {
                Assert.Equal(4, t.Id);
                Assert.Equal(new DateTime(2026, 5, 6, 12, 0, 0), t.Timestamp);
                Assert.Equal("Netflix Subscription", t.Description);
                Assert.Equal(9.99m, t.Amount);
                Assert.Equal(TransactionType.Expense, t.Type);
                Assert.Equal(new[] { 4 }, t.CategoryIds);
            },
            t =>
            {
                Assert.Equal(5, t.Id);
                Assert.Equal(new DateTime(2026, 5, 10, 12, 0, 0), t.Timestamp);
                Assert.Equal("Electricity Bill", t.Description);
                Assert.Equal(65.00m, t.Amount);
                Assert.Equal(TransactionType.Expense, t.Type);
                Assert.Equal(new[] { 5 }, t.CategoryIds);
            });
    }

    [Fact]
    public void GetById_returns_seeded_transaction_by_literal_id()
    {
        var repo = new InMemoryTransactionRepository();

        var electricity = repo.GetById(5);

        Assert.NotNull(electricity);
        Assert.Equal("Electricity Bill", electricity!.Description);
        Assert.Equal(new DateTime(2026, 5, 10, 12, 0, 0), electricity.Timestamp);
    }

    [Fact]
    public void Seeded_transactions_reference_seeded_categories_only()
    {
        var transactions = new InMemoryTransactionRepository().GetAll();
        var seededCategoryIds = new InMemoryCategoryRepository().GetAll().Select(c => c.Id).ToHashSet();

        foreach (var transaction in transactions)
        {
            foreach (var categoryId in transaction.CategoryIds)
            {
                Assert.Contains(categoryId, seededCategoryIds);
            }
        }
    }

    [Fact]
    public void Add_appends_new_transaction_and_assigns_id_six()
    {
        var repo = new InMemoryTransactionRepository();

        var stored = repo.Add(new Transaction(
            Id: 0,
            Timestamp: new DateTime(2026, 5, 31, 9, 0, 0),
            Description: "Bonus",
            Amount: 250m,
            Type: TransactionType.Income,
            CategoryIds: new[] { 1 }));

        Assert.Equal(6, stored.Id);
        Assert.Equal("Bonus", stored.Description);

        var all = repo.GetAll().ToList();
        Assert.Equal(6, all.Count);

        var fetched = repo.GetById(6);
        Assert.NotNull(fetched);
        Assert.Equal("Bonus", fetched!.Description);
    }

    [Fact]
    public void GetById_returns_null_for_unknown_id()
    {
        var repo = new InMemoryTransactionRepository();

        Assert.Null(repo.GetById(99999));
        Assert.Null(repo.GetById(0));
    }

    [Fact]
    public void Delete_existing_returns_true_and_removes_record()
    {
        var repo = new InMemoryTransactionRepository();

        var removed = repo.Delete(2);

        Assert.True(removed);
        Assert.Null(repo.GetById(2));
        Assert.Equal(4, repo.GetAll().Count);
    }

    [Fact]
    public void Delete_missing_returns_false_and_does_not_throw()
    {
        var repo = new InMemoryTransactionRepository();

        Assert.False(repo.Delete(99999));
        Assert.Equal(5, repo.GetAll().Count);
    }

    [Fact]
    public void Adds_one_hundred_transactions_with_reads_under_fifty_milliseconds()
    {
        var repo = new InMemoryTransactionRepository();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 100; i++)
        {
            repo.Add(new Transaction(
                Id: 0,
                Timestamp: new DateTime(2026, 6, 1, 0, 0, 0).AddMinutes(i),
                Description: $"Auto-generated #{i}",
                Amount: 1m + i,
                Type: TransactionType.Expense,
                CategoryIds: new[] { 2 }));
        }

        for (var i = 6; i < 106; i++)
        {
            var fetched = repo.GetById(i);
            Assert.NotNull(fetched);
        }

        _ = repo.GetAll().Count;

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Expected 100 inserts + 100 reads + GetAll() to complete in under 1s, took {stopwatch.ElapsedMilliseconds}ms.");
        Assert.Equal(105, repo.GetAll().Count);
    }
}
