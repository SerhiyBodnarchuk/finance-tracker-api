using Finance.Business.Dtos.Transactions;
using Finance.Business.Mappers;
using Finance.Data.Repositories;

namespace Finance.Business.Services;

public sealed class TransactionService(
    ITransactionRepository transactions,
    ICategoryRepository categories) : ITransactionService
{
    public IReadOnlyCollection<TransactionResponse> GetAll()
    {
        var categoriesById = categories.GetAll().ToDictionary(c => c.Id);
        return transactions.GetAll()
            .Select(t => t.ToResponse(categoriesById))
            .ToList()
            .AsReadOnly();
    }

    public TransactionResponse? GetById(int id)
    {
        var transaction = transactions.GetById(id);
        if (transaction is null) return null;

        var categoriesById = categories.GetAll().ToDictionary(c => c.Id);
        return transaction.ToResponse(categoriesById);
    }

    public TransactionResponse Create(TransactionCreateRequest request)
    {
        var stored = transactions.Add(request.ToEntity(assignedId: 0));
        var categoriesById = categories.GetAll().ToDictionary(c => c.Id);
        return stored.ToResponse(categoriesById);
    }

    public bool Delete(int id) => transactions.Delete(id);
}
