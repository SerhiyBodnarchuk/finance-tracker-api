using Finance.Business.Dtos.Transactions;
using Finance.Data.Models;

namespace Finance.Business.Mappers;

public static class TransactionMapper
{
    public static TransactionResponse ToResponse(
        this Transaction transaction,
        IReadOnlyDictionary<int, Category> categoriesById)
    {
        var categories = new List<CategorySummary>(transaction.CategoryIds.Count);
        foreach (var id in transaction.CategoryIds)
        {
            if (!categoriesById.TryGetValue(id, out var category))
            {
                throw new KeyNotFoundException(
                    $"Transaction {transaction.Id} references unknown category {id}.");
            }
            categories.Add(new CategorySummary(category.Id, category.Name));
        }

        return new TransactionResponse(
            transaction.Id,
            transaction.Timestamp,
            transaction.Description,
            transaction.Amount,
            transaction.Type,
            categories.AsReadOnly());
    }

    public static Transaction ToEntity(this TransactionCreateRequest request, int assignedId) =>
        new(
            assignedId,
            request.Timestamp,
            request.Description,
            request.Amount,
            request.Type,
            request.CategoryIds);
}
