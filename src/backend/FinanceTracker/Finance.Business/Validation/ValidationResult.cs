namespace Finance.Business.Validation;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<ValidationError>());

    public static ValidationResult Failure(params ValidationError[] errors) =>
        new(false, errors);
}
