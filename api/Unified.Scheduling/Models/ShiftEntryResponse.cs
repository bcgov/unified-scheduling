namespace Unified.Scheduling.Models;

public sealed record ShiftEntryResponse
{
    public int Id { get; init; }

    public int? ShiftSeriesId { get; init; }

    public int EventId { get; init; }

    public string? Title { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public string? StatusTypeCode { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
