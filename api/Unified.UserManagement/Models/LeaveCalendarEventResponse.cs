namespace Unified.UserManagement.Models;

public sealed record LeaveCalendarEventResponse
{
    public required string Id { get; init; }

    public int LeaveId { get; init; }

    public int EventId { get; init; }

    public required Guid UserId { get; init; }

    public required string Type { get; init; }

    public required string SourceModule { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public required DateTimeOffset Start { get; init; }

    public DateTimeOffset? End { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public required string EventTypeCode { get; init; }

    public required string StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }

    public int LeaveTypeId { get; init; }

    public required string LeaveTypeCode { get; init; }

    public bool IsPaid { get; init; }
}
