namespace Unified.UserManagement.Models;

public sealed record UserQueryParams
{
    public string? Search { get; init; }
    public bool? IsEnabled { get; init; }
    public int? LocationId { get; init; }
}
