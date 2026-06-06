using Finance.Mcp.Schema;

namespace Finance.Mcp.Logging;

public class McpIterationLogger : IMcpIterationLogger
{
    private const int MaxEntries = 500;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private readonly string _logPath;

    public McpIterationLogger(string logPath)
    {
        _logPath = logPath;
    }

    public McpIterationLogger() : this(DefaultLogPath()) { }

    public void Log(IterationLogEntry entry)
    {
        var block = FormatEntry(entry);
        File.AppendAllText(_logPath, block);
        ApplyRetention();
    }

    private void ApplyRetention()
    {
        if (!File.Exists(_logPath))
            return;

        var content = File.ReadAllText(_logPath);
        var entries = ParseEntries(content);
        var cutoff = DateTime.UtcNow - MaxAge;

        var surviving = entries
            .Where(e => e.parsedTimestamp >= cutoff)
            .TakeLast(MaxEntries)
            .Select(e => e.raw)
            .ToList();

        if (surviving.Count < entries.Count)
            File.WriteAllText(_logPath, string.Join(string.Empty, surviving));
    }

    private static List<(string raw, DateTime parsedTimestamp)> ParseEntries(string content)
    {
        var results = new List<(string, DateTime)>();
        const string start = "--- MCP ENTRY START ---\n";
        const string end = "--- MCP ENTRY END ---\n";
        int pos = 0;

        while (true)
        {
            int s = content.IndexOf(start, pos, StringComparison.Ordinal);
            if (s < 0) break;
            int e = content.IndexOf(end, s, StringComparison.Ordinal);
            if (e < 0) break;
            e += end.Length;

            var raw = content[s..e];
            var ts = ParseTimestamp(raw);
            results.Add((raw, ts));
            pos = e;
        }

        return results;
    }

    private static DateTime ParseTimestamp(string raw)
    {
        const string prefix = "Timestamp: ";
        var line = raw.Split('\n').FirstOrDefault(l => l.StartsWith(prefix));
        if (line is null) return DateTime.UtcNow;
        return DateTime.TryParse(line[prefix.Length..].Trim(), out var dt) ? dt : DateTime.UtcNow;
    }

    private static string FormatEntry(IterationLogEntry e) =>
        $"--- MCP ENTRY START ---\n" +
        $"Timestamp: {e.Timestamp}\n" +
        $"Prompt: {e.Prompt}\n" +
        $"ContextSnapshotHash: {e.ContextSnapshotHash}\n" +
        $"ModelName: {e.ModelName}\n" +
        $"AgentOutput: {e.AgentOutput}\n" +
        $"AcceptedDiff: {e.AcceptedDiff ?? "null"}\n" +
        $"DecisionReason: {e.DecisionReason}\n" +
        $"--- MCP ENTRY END ---\n";

    private static string DefaultLogPath()
    {
        var repo = FindRepoRoot();
        return repo is not null
            ? Path.Combine(repo, "ai-artifacts", "mcp_iteration_log.txt")
            : Path.Combine(AppContext.BaseDirectory, "mcp_iteration_log.txt");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
