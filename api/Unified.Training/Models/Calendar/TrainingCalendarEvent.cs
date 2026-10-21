namespace Unified.Training.Models.Calendar;

public sealed record TrainingCalendarEvent
{
    public required string Id { get; init; }
    public int UserTrainingId { get; init; }
    public Guid UserId { get; init; }
    public int TrainingId { get; init; }
    public required string Type { get; init; }
    public required string SourceModule { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public bool AllDay { get; init; }
    public bool IsReadOnly { get; init; }
    public int? LocationId { get; init; }
}
