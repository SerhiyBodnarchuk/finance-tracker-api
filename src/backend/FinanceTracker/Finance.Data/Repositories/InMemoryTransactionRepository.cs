using Finance.Data.Models;

namespace Finance.Data.Repositories;

public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions;
    private int _nextId;

    public InMemoryTransactionRepository()
    {
        _transactions = new List<Transaction>
        {
            new(1, new DateTime(2026, 5, 1, 12, 0, 0), "Monthly salary", 1200.00m, TransactionType.Income, new[] { 1 }),
            new(2, new DateTime(2026, 5, 4, 12, 0, 0), "Uber Trip", 14.20m, TransactionType.Expense, new[] { 3 }),
            new(3, new DateTime(2026, 5, 5, 12, 0, 0), "Silpo Market", 32.10m, TransactionType.Expense, new[] { 2 }),
            new(4, new DateTime(2026, 5, 6, 12, 0, 0), "Netflix Subscription", 9.99m, TransactionType.Expense, new[] { 4 }),
            new(5, new DateTime(2026, 5, 10, 12, 0, 0), "Electricity Bill", 65.00m, TransactionType.Expense, new[] { 5 })
        };
        _nextId = 6;
    }

    public IReadOnlyCollection<Transaction> GetAll()
    {
        return _transactions.AsReadOnly();
    }

    public Transaction? GetById(int id)
    {
        return _transactions.FirstOrDefault(t => t.Id == id);
    }

    public Transaction Add(Transaction transaction)
    {
        var stored = transaction with { Id = _nextId };
        _transactions.Add(stored);
        _nextId++;
        return stored;
    }

    public bool Delete(int id)
    {
        var existing = _transactions.FirstOrDefault(t => t.Id == id);
        if (existing is null) 
            return false;
        _transactions.Remove(existing);
        return true;
    }
}