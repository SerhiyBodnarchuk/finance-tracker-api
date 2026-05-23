using Finance.Business.Dtos.Transactions;

namespace Finance.Business.Services;

public interface ITransactionService
{
    IReadOnlyCollection<TransactionResponse> GetAll();
    TransactionResponse? GetById(int id);
    TransactionResponse Create(TransactionCreateRequest request);
    bool Delete(int id);
}
