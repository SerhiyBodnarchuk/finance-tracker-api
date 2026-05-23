using Finance.Business.Dtos.Transactions;
using Finance.Business.Validation;

namespace Finance.Api.Infrastructure.Validators;

public interface ITransactionValidator
{
    ValidationResult ValidateForCreate(TransactionCreateRequest request);
}
