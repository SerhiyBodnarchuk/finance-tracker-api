using Finance.Business.Validation;

namespace Finance.Business.Services.Reports;

public sealed class ReportValidationException : Exception
{
    public ReportValidationException(IReadOnlyList<ValidationError> errors)
        : base($"Report request is invalid: {errors.Count} error(s).")
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
