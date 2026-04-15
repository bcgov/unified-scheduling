namespace Unified.Core.Models;

public sealed record class LookupCodeResponse
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }
}
