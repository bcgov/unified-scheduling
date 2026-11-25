namespace Unified.UserManagement.Models;

public sealed record UserQueryParams
{
    public string? Search { get; init; }
    public bool? IsEnabled { get; init; }
    public int? LocationId { get; init; }
}

public sealed record PagedUserQueryParams
{
    public string? Search { get; init; }
    public bool? IsEnabled { get; init; }
    public IReadOnlyList<int>? LocationId { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

public sealed record PagedUserResponse
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required IReadOnlyList<UserResponse> Data { get; init; }
}
