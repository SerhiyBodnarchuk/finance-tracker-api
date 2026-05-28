using System.Net;
using System.Net.Http.Json;
using Finance.Business;
using Finance.Business.Dtos.Transactions;
using Xunit;

namespace Finance.Api.IntegrationTests;

public class TransactionsExportTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public TransactionsExportTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<HttpResponseMessage> ExportAsync(string? accept = null)
    {
        using var client = _fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/transactions/export");
        if (accept is not null)
            request.Headers.TryAddWithoutValidation("Accept", accept);
        return await client.SendAsync(request);
    }

    // --- US1: JSON export ---

    [Fact]
    public async Task AcceptJson_Returns200()
    {
        using var response = await ExportAsync("application/json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptJson_ContentTypeIsJson()
    {
        using var response = await ExportAsync("application/json");
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AcceptJson_ContentDispositionIsAttachment()
    {
        using var response = await ExportAsync("application/json");
        var cd = response.Content.Headers.ContentDisposition;
        Assert.NotNull(cd);
        Assert.Equal("attachment", cd!.DispositionType);
        Assert.Equal("transactions.json", cd.FileName);
    }

    [Fact]
    public async Task AcceptJson_BodyIsNonEmptyJsonArray()
    {
        using var response = await ExportAsync("application/json");
        var body = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task AcceptWildcard_DefaultsToJson()
    {
        using var response = await ExportAsync("*/*");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NoAcceptHeader_DefaultsToJson()
    {
        using var response = await ExportAsync(accept: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- US2: CSV export ---

    [Fact]
    public async Task AcceptCsv_Returns200()
    {
        using var response = await ExportAsync("text/csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptCsv_ContentTypeIsCsv()
    {
        using var response = await ExportAsync("text/csv");
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AcceptCsv_ContentDispositionIsAttachment()
    {
        using var response = await ExportAsync("text/csv");
        var cd = response.Content.Headers.ContentDisposition;
        Assert.NotNull(cd);
        Assert.Equal("attachment", cd!.DispositionType);
        Assert.Equal("transactions.csv", cd.FileName);
    }

    [Fact]
    public async Task AcceptCsv_BodyStartsWithHeaderRow()
    {
        using var response = await ExportAsync("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        var firstLine = body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.Equal("Id,Description,Amount,Type,Timestamp,CategoryIds", firstLine);
    }

    [Fact]
    public async Task AcceptCsv_BodyContainsAllSeededTransactions()
    {
        using var response = await ExportAsync("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, lines.Length); // 1 header + 5 seeded transactions
    }

    [Fact]
    public async Task QualityWeighted_JsonHigherQuality_ReturnsJson()
    {
        using var response = await ExportAsync("text/csv;q=0.5, application/json;q=1.0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- US3: 406 rejection ---

    [Fact]
    public async Task AcceptXml_Returns406()
    {
        using var response = await ExportAsync("application/xml");
        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }
}
