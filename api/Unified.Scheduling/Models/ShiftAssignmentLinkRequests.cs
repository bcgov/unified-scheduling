namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryLinkRequest
{
    public int AssignmentEntryId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}

public sealed record AssignmentSeriesLinkRequest
{
    public int AssignmentSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}

public sealed record ShiftEntryLinkRequest
{
    public int ShiftEntryId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}

public sealed record ShiftSeriesLinkRequest
{
    public int ShiftSeriesId { get; init; }

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
