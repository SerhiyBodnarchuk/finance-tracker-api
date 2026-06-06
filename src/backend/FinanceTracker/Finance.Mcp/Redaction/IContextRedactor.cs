using Finance.Mcp.Schema;

namespace Finance.Mcp.Redaction;

public interface IContextRedactor
{
    McpContext Redact(McpContext context);
}
