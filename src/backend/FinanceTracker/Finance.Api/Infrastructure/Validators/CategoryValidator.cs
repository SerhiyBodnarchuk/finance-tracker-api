using Finance.Business.Dtos.Categories;
using Finance.Business.Validation;

namespace Finance.Api.Infrastructure.Validators;

public sealed class CategoryValidator : ICategoryValidator
{
    public ValidationResult ValidateForCreate(CategoryCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationResult.Failure(
                new ValidationError("name", "Name is required."));
        }

        return ValidationResult.Success;
    }
}
