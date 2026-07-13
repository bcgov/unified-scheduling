namespace Unified.Core.Models;

public record LookupCodeRequest
{
    public required string Code { get; init; }

    public required string Description { get; init; }
}