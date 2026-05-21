using System.Text.Json;
using Finance.Business;
using Finance.Business.Dtos.Reports;
using Finance.Business.Enums;
using Xunit;

namespace Finance.Business.UnitTests.Dtos;

public class ReportRequestRoundTripTests
{
    [Fact]
    public void Period_request_from_README_round_trips_through_envelope_and_typed_payload()
    {
        const string readmePayload = """
            {
              "type": "Period",
              "data": {
                "start": "2026-05-01",
                "end": "2026-05-31"
              }
            }
            """;

        var request = JsonSerializer.Deserialize<ReportRequest>(
            readmePayload, JsonSerializationOptions.Default);
        Assert.NotNull(request);
        Assert.Equal(ReportType.Period, request!.Type);

        var data = request.Data.Deserialize<PeriodReportData>(JsonSerializationOptions.Default);
        Assert.NotNull(data);
        Assert.Equal(new DateOnly(2026, 5, 1), data!.Start);
        Assert.Equal(new DateOnly(2026, 5, 31), data.End);

        // Serialize back and confirm the envelope keeps "type": "Period"
        // and a "data" object with the same start/end values.
        var roundTripJson = JsonSerializer.Serialize(request, JsonSerializationOptions.Default);
        using var roundTripDoc = JsonDocument.Parse(roundTripJson);
        Assert.Equal("Period", roundTripDoc.RootElement.GetProperty("type").GetString());
        var dataElement = roundTripDoc.RootElement.GetProperty("data");
        Assert.Equal("2026-05-01", dataElement.GetProperty("start").GetString());
        Assert.Equal("2026-05-31", dataElement.GetProperty("end").GetString());
    }

    [Fact]
    public void Unknown_ReportType_value_throws_JsonException_at_binding()
    {
        // "Quarter" is NOT in the ReportType enum (which has only Period).
        const string unknownTypePayload = """
            {
              "type": "Quarter",
              "data": {}
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ReportRequest>(
                unknownTypePayload, JsonSerializationOptions.Default));
    }
}
