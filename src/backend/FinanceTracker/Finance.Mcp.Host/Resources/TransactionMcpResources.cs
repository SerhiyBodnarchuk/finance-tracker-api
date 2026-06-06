using System.Text.Json;
using Finance.Business.Services;
using ModelContextProtocol.Server;

namespace Finance.Mcp.Host.Resources;

[McpServerResourceType]
public class TransactionMcpResources(ITransactionService transactionService)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [McpServerResource(UriTemplate = "finance://transactions", Name = "All Transactions", MimeType = "application/json")]
    public string GetAllTransactions() =>
        JsonSerializer.Serialize(transactionService.GetAll(), Json);

    [McpServerResource(UriTemplate = "finance://transactions/{id}", Name = "Transaction by ID", MimeType = "application/json")]
    public string GetTransactionById(int id)
    {
        var transaction = transactionService.GetById(id);
        if (transaction is null)
            return JsonSerializer.Serialize(new { error = $"Transaction {id} not found" }, Json);

        return JsonSerializer.Serialize(transaction, Json);
    }
}
