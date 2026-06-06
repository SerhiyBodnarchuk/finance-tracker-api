using Finance.Mcp.Schema;

namespace Finance.Mcp.Logging;

public interface IMcpIterationLogger
{
    void Log(IterationLogEntry entry);
}
