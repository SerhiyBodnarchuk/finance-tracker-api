using System.Net;
using System.Net.Http.Json;
using Finance.Business;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Xunit;

namespace Finance.Api.IntegrationTests;

public class ReportsEndpointTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public ReportsEndpointTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static object PeriodEnvelope(string start, string end) => new
    {
        type = "Period",
        data = new { start, end }
    };

    [Fact]
    public async Task POST_reports_period_returns_200_with_documented_seed_window_numbers()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.PostAsJsonAsync(
            "/api/reports",
            PeriodEnvelope("2026-05-01", "2026-05-31"),
            JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReportResult>(JsonSerializationOptions.Default);
        Assert.NotNull(body);
        Assert.Equal(ReportType.Period, body!.Type);
        Assert.Equal("2026-05-01..2026-05-31", body.Period);
        Assert.Equal(1200.00m, body.IncomeTotal);
        Assert.Equal(121.29m, body.ExpenseTotal);
        Assert.Equal(1078.71m, body.NetTotal);
        Assert.Equal(5, body.CategoryBreakdown.Count);
    }

    [Fact]
    public async Task POST_reports_iso_week_returns_400_with_unsupported_type()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.PostAsJsonAsync(
            "/api/reports",
            new { type = "IsoWeek", data = new { week = "2026-W19" } },
            JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsupported", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task POST_reports_unknown_type_returns_400()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.PostAsJsonAsync(
            "/api/reports",
            new { type = "Month", data = new { } },
            JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_reports_period_with_start_after_end_returns_400()
    {
        using var client = _fixture.CreateConfiguredClient();

        var response = await client.PostAsJsonAsync(
            "/api/reports",
            PeriodEnvelope("2026-05-31", "2026-05-01"),
            JsonSerializationOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("less than or equal", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task POST_reports_period_twice_returns_byte_identical_bodies()
    {
        using var client = _fixture.CreateConfiguredClient();

        var envelope = PeriodEnvelope("2026-05-01", "2026-05-31");

        var first  = await client.PostAsJsonAsync("/api/reports", envelope, JsonSerializationOptions.Default);
        var second = await client.PostAsJsonAsync("/api/reports", envelope, JsonSerializationOptions.Default);

        var firstBody  = await first .Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(firstBody, secondBody);
    }
}
