using Finance.Business.Dtos.Categories;
using Finance.Business.Validation;

namespace Finance.Api.Infrastructure.Validators;

public interface ICategoryValidator
{
    ValidationResult ValidateForCreate(CategoryCreateRequest request);
}
