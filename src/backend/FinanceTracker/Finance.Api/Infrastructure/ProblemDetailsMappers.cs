using Finance.Business.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Infrastructure;

public static class ProblemDetailsMappers
{
    public static ValidationProblemDetails ToValidationProblem(ValidationResult result) =>
        ToValidationProblem(result.Errors);

    public static ValidationProblemDetails ToValidationProblem(IReadOnlyList<ValidationError> errors)
    {
        var errorsByField = errors
            .GroupBy(e => e.Field, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

        return new ValidationProblemDetails(errorsByField)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }
}
