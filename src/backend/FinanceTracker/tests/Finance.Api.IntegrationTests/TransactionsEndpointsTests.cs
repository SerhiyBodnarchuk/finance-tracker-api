using System.Net;
using System.Net.Http.Json;
using Finance.Business;
using Finance.Business.Dtos.Transactions;
using Xunit;

namespace Finance.Api.IntegrationTests;

public class TransactionsEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public TransactionsEndpointsTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GET_transactions_returns_200_with_five_seeded_records()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, body.Select(t => t.Id).ToArray());
        Assert.Equal("Monthly salary", body[0].Description);
    }

    [Fact]
    public async Task GET_transactions_by_id_returns_200_for_seeded_id()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/transactions/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.Equal("Silpo Market", body!.Description);
        Assert.Single(body.Categories);
        Assert.Equal("Groceries", body.Categories[0].Name);
    }

    [Fact]
    public async Task GET_transactions_by_id_returns_404_for_unknown_id()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/transactions/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_transactions_returns_201_with_location_for_valid_body()
    {
        // Isolated fixture so the runtime-added record doesn't bleed into other tests.
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var request = new TransactionCreateRequest(
            Timestamp: new DateTime(2026, 5, 31, 12, 0, 0),
            Description: "Coffee",
            Amount: 4.50m,
            Type: Finance.Data.Models.TransactionType.Expense,
            CategoryIds: new[] { 2 });

        var response = await client.PostAsJsonAsync("/api/transactions", request, JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith("/api/Transactions/6", response.Headers.Location!.ToString());

        var stored = await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonSerializationOptions.Default);
        Assert.NotNull(stored);
        Assert.Equal(6, stored!.Id);
        Assert.Equal("Coffee", stored.Description);
    }

    [Fact]
    public async Task POST_transactions_returns_400_with_amount_error_for_zero_amount()
    {
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var request = new TransactionCreateRequest(
            new DateTime(2026, 5, 31, 12, 0, 0), "x", 0m,
            Finance.Data.Models.TransactionType.Expense, new[] { 2 });

        var response = await client.PostAsJsonAsync("/api/transactions", request, JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("amount", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DELETE_transactions_id_returns_204_then_404_on_second_call()
    {
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var first = await client.DeleteAsync("/api/transactions/4");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.DeleteAsync("/api/transactions/4");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }
}
