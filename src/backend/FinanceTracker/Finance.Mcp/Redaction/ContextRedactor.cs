using Finance.Mcp.Schema;

namespace Finance.Mcp.Redaction;

public class ContextRedactor : IContextRedactor
{
    public static readonly HashSet<string> ProhibitedKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "secret", "token", "email", "name",
            "accountnumber", "iban", "ssn", "description"
        };

    public McpContext Redact(McpContext context)
    {
        var redactedTransactions = context.PendingTransactions
            .Select(t => t with { Description = "[REDACTED]" })
            .ToList()
            .AsReadOnly();

        return context with { PendingTransactions = redactedTransactions };
    }
}
