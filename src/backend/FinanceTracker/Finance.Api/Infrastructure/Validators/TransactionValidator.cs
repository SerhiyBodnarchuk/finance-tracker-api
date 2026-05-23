using Finance.Business.Dtos.Categories;
using Finance.Business.Dtos.Transactions;
using Finance.Business.Services;
using Finance.Business.Validation;
using Finance.Data.Models;

namespace Finance.Api.Infrastructure.Validators;

public sealed class TransactionValidator(ICategoryService categories) : ITransactionValidator
{
    public ValidationResult ValidateForCreate(TransactionCreateRequest request)
    {
        var errors = new List<ValidationError>();

        if (request.Amount <= 0m)
            errors.Add(new ValidationError("amount", "Amount must be greater than zero."));

        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add(new ValidationError("description", "Description is required."));

        if (request.CategoryIds is null || request.CategoryIds.Count == 0)
        {
            errors.Add(new ValidationError("categoryIds", "At least one category id is required."));
            return errors.Count > 0 ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
        }

        var resolved = new List<CategoryResponse>(request.CategoryIds.Count);
        for (var i = 0; i < request.CategoryIds.Count; i++)
        {
            var id = request.CategoryIds[i];
            var category = categories.GetById(id);
            if (category is null)
                errors.Add(new ValidationError($"categoryIds[{i}]", $"Category id {id} does not exist."));
            else
                resolved.Add(category);
        }

        if (resolved.Count == request.CategoryIds.Count)
        {
            for (var i = 0; i < resolved.Count; i++)
            {
                if (!IsCompatible(request.Type, resolved[i].Type))
                {
                    errors.Add(new ValidationError(
                        $"categoryIds[{i}]",
                        $"Category '{resolved[i].Name}' ({resolved[i].Type}) is not compatible with transaction type {request.Type}."));
                }
            }
        }

        return errors.Count > 0 ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
    }

    private static bool IsCompatible(TransactionType transactionType, CategoryType categoryType) =>
        categoryType == CategoryType.Both ||
        (transactionType == TransactionType.Income  && categoryType == CategoryType.Income) ||
        (transactionType == TransactionType.Expense && categoryType == CategoryType.Expense);
}
