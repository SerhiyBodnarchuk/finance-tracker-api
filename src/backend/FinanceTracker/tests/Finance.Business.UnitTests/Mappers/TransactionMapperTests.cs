using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Mappers;
using Finance.Data.Models;
using Xunit;

namespace Finance.Business.UnitTests.Mappers;

public class TransactionMapperTests
{
    [Fact]
    public void ToResponse_maps_scalar_fields_and_builds_Categories_in_CategoryIds_order()
    {
        var transaction = new Transaction(
            Id: 4,
            Timestamp: new DateTime(2026, 5, 5, 18, 42, 0),
            Description: "Grocery + health-food run",
            Amount: 42.10m,
            Type: TransactionType.Expense,
            CategoryIds: new[] { 5, 2 });

        var categoriesById = new Dictionary<int, Category>
        {
            [2] = new(2, "Groceries", CategoryType.Expense),
            [5] = new(5, "Health Food", CategoryType.Expense)
        };

        var response = transaction.ToResponse(categoriesById);

        Assert.Equal(4, response.Id);
        Assert.Equal(transaction.Timestamp, response.Timestamp);
        Assert.Equal("Grocery + health-food run", response.Description);
        Assert.Equal(42.10m, response.Amount);
        Assert.Equal(TransactionType.Expense, response.Type);
        Assert.Equal(
            new[]
            {
                new CategorySummary(5, "Health Food"),
                new CategorySummary(2, "Groceries")
            },
            response.Categories);
    }

    [Fact]
    public void ToResponse_throws_KeyNotFoundException_when_category_lookup_misses()
    {
        var transaction = new Transaction(
            Id: 9,
            Timestamp: new DateTime(2026, 5, 5, 18, 42, 0),
            Description: "Something",
            Amount: 10m,
            Type: TransactionType.Expense,
            CategoryIds: new[] { 99 });

        var emptyLookup = new Dictionary<int, Category>();

        var ex = Assert.Throws<KeyNotFoundException>(() => transaction.ToResponse(emptyLookup));
        Assert.Contains("Transaction 9", ex.Message);
        Assert.Contains("category 99", ex.Message);
    }

    [Fact]
    public void ToEntity_carries_assigned_id_and_preserves_CategoryIds_from_README_example()
    {
        const string readmePayload = """
            {
              "timestamp": "2026-05-07T09:15:00",
              "description": "Coffee",
              "amount": 4.50,
              "transactionType": "Expense",
              "categoryIds": [3]
            }
            """;

        var request = JsonSerializer.Deserialize<TransactionCreateRequest>(
            readmePayload, JsonSerializationOptions.Default);
        Assert.NotNull(request);

        var entity = request!.ToEntity(assignedId: 123);

        Assert.Equal(123, entity.Id);
        Assert.Equal(new DateTime(2026, 5, 7, 9, 15, 0), entity.Timestamp);
        Assert.Equal("Coffee", entity.Description);
        Assert.Equal(4.50m, entity.Amount);
        Assert.Equal(TransactionType.Expense, entity.Type);
        Assert.Equal(new[] { 3 }, entity.CategoryIds);
    }
}
