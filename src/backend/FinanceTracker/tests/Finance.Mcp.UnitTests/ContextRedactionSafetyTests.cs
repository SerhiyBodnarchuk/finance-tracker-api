using System.Text.Json;
using Finance.Business.Services;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Finance.Mcp.Schema;
using Moq;
using Xunit;

namespace Finance.Mcp.UnitTests;

public class ContextRedactionSafetyTests
{
    private static McpServer BuildServer() =>
        new(new McpContextStore(), new ContextRedactor(), Mock.Of<IMcpIterationLogger>(), Mock.Of<ICategoryService>(), Mock.Of<ITransactionService>());

    private static McpContext BuildContextWithSensitiveDescriptions() =>
        new(
            Id: "ctx-safety-test",
            Version: 1,
            Ttl: DateTime.UtcNow.AddMinutes(30),
            CategoryMappings: new[] { new CategoryMappingItem(1, "Food", "Expense") },
            PendingTransactions: new[]
            {
                new PendingTransactionItem(1, 10m, "Expense", DateTime.UtcNow, "secret-value"),
                new PendingTransactionItem(2, 20m, "Expense", DateTime.UtcNow, "my-password-123")
            },
            Reconciliation: new ReconciliationContext(
                new[] { 1 },
                new Dictionary<int, IReadOnlyList<int>>(),
                ContextStatus.Active),
            Status: ContextStatus.Active,
            CreatedAt: DateTime.UtcNow);

    // US3: parameterised — no prohibited key appears in any serialised snapshot
    [Theory]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("email")]
    [InlineData("accountnumber")]
    [InlineData("iban")]
    [InlineData("ssn")]
    public void Snapshot_DoesNotContainProhibitedKey(string prohibitedKey)
    {
        var store = new McpContextStore();
        var context = BuildContextWithSensitiveDescriptions();
        store.Set(context.Id, context);

        var server = new McpServer(store, new ContextRedactor(), Mock.Of<IMcpIterationLogger>(), Mock.Of<ICategoryService>(), Mock.Of<ITransactionService>());
        var snapshot = server.TakeSnapshot(context.Id);

        // Case-insensitive check: the key must not appear as a JSON property name
        var json = snapshot.RedactedJson;
        var lowerJson = json.ToLowerInvariant();
        var lowerKey = $"\"{prohibitedKey.ToLowerInvariant()}\"";

        Assert.DoesNotContain(lowerKey, lowerJson);
    }

    // US3: description fields on all transactions are exactly "[REDACTED]"
    [Fact]
    public void Snapshot_AllDescriptionsAreRedacted()
    {
        var store = new McpContextStore();
        var context = BuildContextWithSensitiveDescriptions();
        store.Set(context.Id, context);
        var server = new McpServer(store, new ContextRedactor(), Mock.Of<IMcpIterationLogger>(), Mock.Of<ICategoryService>(), Mock.Of<ITransactionService>());

        var snapshot = server.TakeSnapshot(context.Id);
        var doc = JsonDocument.Parse(snapshot.RedactedJson);
        var transactions = doc.RootElement.GetProperty("pendingTransactions");

        foreach (var txn in transactions.EnumerateArray())
        {
            var desc = txn.GetProperty("description").GetString();
            Assert.Equal("[REDACTED]", desc);
        }
    }

    // Safety: ProhibitedKeys set contains all 9 expected keys
    [Fact]
    public void ContextRedactor_ProhibitedKeys_ContainsAllRequiredKeys()
    {
        var expected = new[]
        {
            "password", "secret", "token", "email", "name",
            "accountnumber", "iban", "ssn", "description"
        };

        foreach (var key in expected)
            Assert.Contains(key, ContextRedactor.ProhibitedKeys, StringComparer.OrdinalIgnoreCase);
    }

    // In-memory context retains original description after redaction
    [Fact]
    public void Redact_DoesNotMutateOriginalContext()
    {
        var redactor = new ContextRedactor();
        var txn = new PendingTransactionItem(1, 10m, "Expense", DateTime.UtcNow, "original text");
        var context = new McpContext(
            "id", 1, DateTime.UtcNow.AddMinutes(30),
            Array.Empty<CategoryMappingItem>(), new[] { txn },
            new ReconciliationContext(new[] { 1 }, new Dictionary<int, IReadOnlyList<int>>(), ContextStatus.Active),
            ContextStatus.Active, DateTime.UtcNow);

        var redacted = redactor.Redact(context);

        Assert.Equal("original text", context.PendingTransactions[0].Description);
        Assert.Equal("[REDACTED]", redacted.PendingTransactions[0].Description);
    }
}
