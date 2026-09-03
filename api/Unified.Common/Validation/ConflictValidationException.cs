namespace Unified.Common.Validation;

public sealed class ConflictValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception(errors.Values.SelectMany(messages => messages).FirstOrDefault() ?? "A conflict occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
