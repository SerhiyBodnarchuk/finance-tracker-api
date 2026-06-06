using Finance.Mcp.Schema;

namespace Finance.Mcp;

public interface IMcpContextStore
{
    McpContext? Get(string contextId);
    void Set(string contextId, McpContext context);
    void Remove(string contextId);
}
