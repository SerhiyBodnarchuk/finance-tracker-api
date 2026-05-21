using Finance.Data.Models;

namespace Finance.Data.Repositories;

public interface ITransactionRepository
{
    IReadOnlyCollection<Transaction> GetAll();
    Transaction? GetById(int id);
    Transaction Add(Transaction transaction);
    bool Delete(int id);
}
