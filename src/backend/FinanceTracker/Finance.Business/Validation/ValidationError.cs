namespace Finance.Business.Validation;

public sealed record ValidationError(string Field, string Message);
