using System.Net;
using System.Net.Http.Json;
using Finance.Business;
using Finance.Business.Dtos.Categories;
using Xunit;

namespace Finance.Api.IntegrationTests;

public class CategoriesEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public CategoriesEndpointsTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GET_categories_returns_200_with_five_seeded_records()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.Equal(5, body!.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, body.Select(c => c.Id).ToArray());
        Assert.Equal("Salary", body[0].Name);
    }

    [Fact]
    public async Task GET_categories_by_id_returns_200_for_seeded_id()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/categories/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.Equal("Groceries", body!.Name);
    }

    [Fact]
    public async Task GET_categories_by_id_returns_404_for_unknown_id()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.GetAsync("/api/categories/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_categories_returns_201_for_valid_body()
    {
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var request = new CategoryCreateRequest("Savings", Finance.Data.Models.CategoryType.Income);

        var response = await client.PostAsJsonAsync("/api/categories", request, JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith("/api/Categories/6", response.Headers.Location!.ToString());

        var stored = await response.Content.ReadFromJsonAsync<CategoryResponse>(JsonSerializationOptions.Default);
        Assert.NotNull(stored);
        Assert.Equal(6, stored!.Id);
        Assert.Equal("Savings", stored.Name);
    }

    [Fact]
    public async Task POST_categories_returns_409_for_case_insensitive_duplicate_name()
    {
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var request = new CategoryCreateRequest("GROCERIES", Finance.Data.Models.CategoryType.Expense);

        var response = await client.PostAsJsonAsync("/api/categories", request, JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Duplicate", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task POST_categories_returns_400_when_type_is_integer_outside_enum_range()
    {
        using var fixture = new ApiTestFixture();
        using var client = fixture.CreateConfiguredClient();

        var rawJson = """{"name":"First_test","type":3}""";
        using var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/categories", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The record must not have been stored.
        var listResponse = await client.GetAsync("/api/categories");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("First_test", listBody);
    }
}
