using FluentValidation.Results;

namespace Unified.Common.Validation;

public static class FluentValidationResultExtensions
{
    public static IDictionary<string, string[]> ToValidationErrors(this ValidationResult validationResult)
    {
        return validationResult
            .Errors.GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct().ToArray()
            );
    }
}
