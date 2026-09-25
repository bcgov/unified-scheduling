namespace Unified.Common.Calendar.Conflicts;

public readonly record struct CalendarConflictEventIdentity(string SourceModule, string EventId)
{
    public const int SourceModuleMaxLength = 200;
    public const int EventIdMaxLength = 200;

    public static CalendarConflictEventIdentity Create(string sourceModule, string eventId)
    {
        if (string.IsNullOrWhiteSpace(sourceModule) || sourceModule.Length > SourceModuleMaxLength)
            throw new InvalidOperationException($"A source module between 1 and {SourceModuleMaxLength} characters is required.");
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > EventIdMaxLength)
            throw new InvalidOperationException($"An event ID between 1 and {EventIdMaxLength} characters is required.");

        return new CalendarConflictEventIdentity(sourceModule, eventId);
    }
}