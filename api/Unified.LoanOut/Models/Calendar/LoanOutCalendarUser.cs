namespace Unified.LoanOut.Models.Calendar;

public sealed record LoanOutCalendarUser
{
    public required Guid Id { get; init; }
    public required string IdirName { get; init; }
    public Guid? IdirId { get; init; }
    public required bool IsEnabled { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public string? Rank { get; init; }
    public string? BadgeNumber { get; init; }
    public required string EmployeeNumber { get; init; }
    public int? HomeLocationId { get; init; }
    public int? TrainingProfileId { get; init; }
    public DateTimeOffset? LastLogin { get; init; }
    public bool PendingRegistration { get; init; }
    public IReadOnlyCollection<LoanOutMetaData> LoanOutMetaData { get; init; } = [];
}

public sealed record LoanOutMetaData
{
    public int LoanOutLocationId { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? RecurrenceRule { get; init; }
}
