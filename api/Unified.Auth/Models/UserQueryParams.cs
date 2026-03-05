namespace Unified.Auth.Models;

public sealed record UserQueryParams
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
