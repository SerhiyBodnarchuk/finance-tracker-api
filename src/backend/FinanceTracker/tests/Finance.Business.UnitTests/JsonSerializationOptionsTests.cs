using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Categories;
using Finance.Data.Models;
using Xunit;

namespace Finance.Business.UnitTests;

public class JsonSerializationOptionsTests
{
    [Fact]
    public void Rejects_integer_value_for_CategoryType_enum_during_deserialization()
    {
        var json = """{"name":"X","type":3}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CategoryCreateRequest>(json, JsonSerializationOptions.Default));
    }

    [Fact]
    public void Rejects_integer_value_for_TransactionType_enum_during_deserialization()
    {
        var json = """{"timestamp":"2026-05-31T12:00:00","description":"x","amount":1,"transactionType":99,"categoryIds":[2]}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Finance.Business.Dtos.Transactions.TransactionCreateRequest>(
                json, JsonSerializationOptions.Default));
    }

    [Fact]
    public void Accepts_named_value_for_CategoryType_enum_during_deserialization()
    {
        var json = """{"name":"X","type":"Income"}""";

        var result = JsonSerializer.Deserialize<CategoryCreateRequest>(json, JsonSerializationOptions.Default);

        Assert.NotNull(result);
        Assert.Equal(CategoryType.Income, result!.Type);
    }

    [Fact]
    public void Rejects_unknown_named_value_for_CategoryType_enum_during_deserialization()
    {
        var json = """{"name":"X","type":"NotARealCategoryType"}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CategoryCreateRequest>(json, JsonSerializationOptions.Default));
    }
}
