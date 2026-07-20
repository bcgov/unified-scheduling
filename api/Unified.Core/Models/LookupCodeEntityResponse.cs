namespace Unified.Core.Models;

public record LookupCodeEntityResponse : LookupCodeResponse
{
    public required int Id { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
