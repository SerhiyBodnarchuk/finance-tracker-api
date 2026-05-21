using Finance.Data.Models;
using Xunit;

namespace Finance.Data.UnitTests;

public class TransactionTests
{
    [Fact]
    public void Construct_with_all_fields_exposes_each_field_as_supplied()
    {
        var timestamp = new DateTime(2026, 5, 7, 9, 15, 0);
        var categoryIds = new[] { 2, 5 };

        var transaction = new Transaction(
            Id: 1,
            Timestamp: timestamp,
            Description: "Coffee + pastry",
            Amount: 4.50m,
            Type: TransactionType.Expense,
            CategoryIds: categoryIds);

        Assert.Equal(1, transaction.Id);
        Assert.Equal(timestamp, transaction.Timestamp);
        Assert.Equal("Coffee + pastry", transaction.Description);
        Assert.Equal(4.50m, transaction.Amount);
        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal(categoryIds, transaction.CategoryIds);
    }

    [Fact]
    public void Two_transactions_with_same_data_but_different_ids_are_not_equal()
    {
        var timestamp = new DateTime(2026, 5, 7, 9, 15, 0);
        IReadOnlyList<int> categoryIds = new[] { 3 };

        var first = new Transaction(1, timestamp, "Coffee", 4.50m, TransactionType.Expense, categoryIds);
        var second = new Transaction(2, timestamp, "Coffee", 4.50m, TransactionType.Expense, categoryIds);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CategoryIds_is_assignable_to_IReadOnlyList_of_int()
    {
        var transaction = new Transaction(
            1,
            new DateTime(2026, 5, 7, 9, 15, 0),
            "Coffee",
            4.50m,
            TransactionType.Expense,
            new[] { 3 });

        Assert.IsAssignableFrom<IReadOnlyList<int>>(transaction.CategoryIds);
    }

    [Fact]
    public void Multi_category_transaction_preserves_CategoryIds_order()
    {
        var transaction = new Transaction(
            1,
            new DateTime(2026, 5, 7, 12, 30, 0),
            "Grocery + health-food run",
            42.10m,
            TransactionType.Expense,
            new[] { 2, 5, 7 });

        Assert.Equal(new[] { 2, 5, 7 }, transaction.CategoryIds);
    }
}
