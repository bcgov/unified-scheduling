using Mapster;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Models.Calendar;

namespace Unified.LoanOut.Mappers;

/// <summary>
/// Maps loan-out entries to calendar events and users to chronologically ordered
/// loan-out metadata. Calendar event fields are mapped directly with Mapster.
/// </summary>
public sealed class LoanOutCalendarMapper
{
    private static readonly TypeAdapterConfig LoanOutCalendarMappingConfig = BuildLoanOutCalendarMappingConfig();

    public LoanOutCalendarEvent ToLoanOutCalendarEvent(LoanOutEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var eventEntity =
            entry.Event ?? throw new InvalidOperationException($"LoanOut entry {entry.Id} has no associated event.");

        return eventEntity.Adapt<LoanOutCalendarEvent>(LoanOutCalendarMappingConfig) with
        {
            Id = $"scheduling.LoanOut-entry.{entry.Id}",
            LoanOutEntryId = entry.Id,
            LoanOutSeriesId = entry.LoanOutSeriesId,
            LoanOutLocationId = entry.LoanOutLocationId,
            EventId = entry.EventId,
            UserIds = entry.Users.Select(user => user.UserId).Distinct().ToList(),
            Type = "scheduling.LoanOut",
        };
    }

    public LoanOutCalendarUser ToLoanOutCalendarUser(User user, IReadOnlyCollection<LoanOutEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(entries);

        var metadata = entries
            .OrderBy(entry =>
                entry.Event?.StartAtUtc
                ?? throw new InvalidOperationException($"LoanOut entry {entry.Id} has no associated event.")
            )
            .Select(entry =>
            {
                var eventEntity =
                    entry.Event
                    ?? throw new InvalidOperationException($"LoanOut entry {entry.Id} has no associated event.");

                return eventEntity.Adapt<LoanOutMetaData>(LoanOutCalendarMappingConfig) with
                {
                    LoanOutLocationId = entry.LoanOutLocationId,
                };
            })
            .ToList();

        return user.Adapt<LoanOutCalendarUser>(LoanOutCalendarMappingConfig) with
        {
            EmployeeNumber = user.EmployeeNumber ?? string.Empty,
            LoanOutMetaData = metadata,
        };
    }

    private static TypeAdapterConfig BuildLoanOutCalendarMappingConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<Event, LoanOutCalendarEvent>()
            .Map(dest => dest.Start, src => src.StartAtUtc)
            .Map(dest => dest.End, src => src.EndAtUtc);

        config
            .NewConfig<Event, LoanOutMetaData>()
            .Map(dest => dest.RecurrenceRule, src => src.EventSeries == null ? null : src.EventSeries.RecurrenceRule);

        config.NewConfig<User, LoanOutCalendarUser>();

        return config;
    }
}
