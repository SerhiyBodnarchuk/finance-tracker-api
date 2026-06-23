using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Transactions;
using Finance.Data.Models;
using Xunit;

namespace Finance.Business.UnitTests.Dtos;

public class TransactionResponseRoundTripTests
{
    [Fact]
    public void Serialize_emits_camelCase_fields_with_transactionType_and_ordered_categories()
    {
        var response = new TransactionResponse(
            Id: 4,
            Timestamp: new DateTime(2026, 5, 5, 18, 42, 0),
            Description: "Grocery + health-food run",
            Amount: 42.10m,
            Type: TransactionType.Expense,
            Categories: new[]
            {
                new CategorySummary(2, "Groceries"),
                new CategorySummary(5, "Health Food")
            });

        var json = JsonSerializer.Serialize(response, JsonSerializationOptions.Default);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(4, root.GetProperty("id").GetInt32());
        Assert.Equal("2026-05-05T18:42:00Z", root.GetProperty("timestamp").GetString());
        Assert.Equal("Grocery + health-food run", root.GetProperty("description").GetString());
        Assert.Equal(42.10m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("Expense", root.GetProperty("transactionType").GetString());

        var categories = root.GetProperty("categories");
        Assert.Equal(2, categories.GetArrayLength());
        Assert.Equal(2, categories[0].GetProperty("id").GetInt32());
        Assert.Equal("Groceries", categories[0].GetProperty("name").GetString());
        Assert.Equal(5, categories[1].GetProperty("id").GetInt32());
        Assert.Equal("Health Food", categories[1].GetProperty("name").GetString());
    }

    [Fact]
    public void Deserialize_back_into_TransactionResponse_round_trips_every_field()
    {
        var original = new TransactionResponse(
            Id: 4,
            Timestamp: new DateTime(2026, 5, 5, 18, 42, 0),
            Description: "Grocery + health-food run",
            Amount: 42.10m,
            Type: TransactionType.Expense,
            Categories: new[]
            {
                new CategorySummary(2, "Groceries"),
                new CategorySummary(5, "Health Food")
            });

        var json = JsonSerializer.Serialize(original, JsonSerializationOptions.Default);
        var roundTripped = JsonSerializer.Deserialize<TransactionResponse>(
            json, JsonSerializationOptions.Default);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped!.Id);
        Assert.Equal(original.Timestamp, roundTripped.Timestamp);
        Assert.Equal(original.Description, roundTripped.Description);
        Assert.Equal(original.Amount, roundTripped.Amount);
        Assert.Equal(original.Type, roundTripped.Type);
        Assert.Equal(original.Categories, roundTripped.Categories);
    }
}
