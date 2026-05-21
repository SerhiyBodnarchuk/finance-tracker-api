using System.Text.Json;
using Finance.Business.Enums;

namespace Finance.Business.Dtos.Reports;

public sealed record ReportRequest(
    ReportType Type,
    JsonElement Data);
