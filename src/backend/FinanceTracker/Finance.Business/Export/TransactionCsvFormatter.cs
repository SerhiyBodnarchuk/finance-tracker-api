using System.Globalization;
using System.Text;
using Finance.Business.Dtos.Transactions;

namespace Finance.Business.Export;

public static class TransactionCsvFormatter
{
    private const string Header = "Id,Description,Amount,Type,Timestamp,CategoryIds";
    private const string Crlf = "\r\n";

    public static string Format(IEnumerable<TransactionResponse> transactions)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append(Crlf);
        foreach (var t in transactions)
            sb.Append(BuildRow(t)).Append(Crlf);
        return sb.ToString();
    }

    private static string BuildRow(TransactionResponse t)
    {
        var categoryIds = string.Join(";", t.Categories.Select(c => c.Id));
        return string.Join(",",
            t.Id.ToString(CultureInfo.InvariantCulture),
            Quote(t.Description),
            t.Amount.ToString(CultureInfo.InvariantCulture),
            t.Type.ToString(),
            t.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
            Quote(categoryIds));
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
