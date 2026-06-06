using System.Collections.Concurrent;
using Finance.Mcp.Schema;

namespace Finance.Mcp;

public class McpContextStore : IMcpContextStore
{
    private readonly ConcurrentDictionary<string, McpContext> _store = new();

    public McpContext? Get(string contextId)
    {
        if (!_store.TryGetValue(contextId, out var context))
            return null;

        if (DateTime.UtcNow >= context.Ttl)
        {
            var expired = context.WithStatus(ContextStatus.Expired);
            _store.TryUpdate(contextId, expired, context);
            return null;
        }

        return context;
    }

    public void Set(string contextId, McpContext context) =>
        _store[contextId] = context;

    public void Remove(string contextId) =>
        _store.TryRemove(contextId, out _);
}
