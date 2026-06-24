namespace Unified.UserManagement.Models;

public sealed record ActingPositionRequestDto
{
    public required string PositionTypeCode { get; init; }

    /// <summary>
    /// Local datetime in yyyy-MM-ddTHH:mm format. Backend converts to UTC using the user's home timezone.
    /// Use midnight (T00:00) for full-day acting positions.
    /// </summary>
    public required string StartDateTime { get; init; }

    /// <summary>
    /// Local datetime in yyyy-MM-ddTHH:mm format. Must be after StartDateTime.
    /// Use midnight (T00:00) for full-day acting positions.
    /// </summary>
    public required string EndDateTime { get; init; }

    public string? Comment { get; init; }
}
