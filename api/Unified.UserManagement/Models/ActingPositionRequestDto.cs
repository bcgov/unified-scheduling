namespace Unified.UserManagement.Models;

public sealed record ActingPositionRequestDto
{
    public required string PositionTypeCode { get; init; }

    /// <summary>
    /// Date in yyyy-MM-dd format. Backend will convert to user's timezone start-of-day.
    /// </summary>
    public required string StartDate { get; init; }

    /// <summary>
    /// Date in yyyy-MM-dd format. Must be on or after StartDate.
    /// </summary>
    public required string EndDate { get; init; }

    public string? Comment { get; init; }
}
