using Mapster;
using Unified.Calendar.Mappings;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Models.Calendar;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Mappings;

/// <summary>
/// Leave specific mapper that wraps the injected common <see cref="CalendarEventMapper"/>: response
/// building delegates the shared calendar <c>Event</c>/<c>EventSeries</c> field mapping (which itself
/// uses Mapster/<see cref="Mapster.TypeAdapterConfig"/> internally) to it and adds only the leave
/// specific properties (<c>Id</c>, <c>LeaveTypeId</c>, <c>LeaveSeriesId</c>/<c>LeaveEntryIds</c>) on
/// top. "/>.
/// </summary>
public sealed class SchedulingLeaveEventMapper(
    CalendarEventMapper calendarEventMapper,
    ITimeZoneService timeZoneService
)
{
    private static readonly TypeAdapterConfig LeaveCalendarEventMappingConfig = BuildLeaveCalendarEventMappingConfig();

    /// <summary>
    /// Builds the calendar-query shaped <see cref="LeaveCalendarEvent"/> for an existing leave entry,
    /// used by <see cref="Unified.Leave.Services.LeaveCalendarDataProvider"/>. Shared <c>Event</c>
    /// fields are copied via Mapster (<see cref="LeaveCalendarEventMappingConfig"/>); the leave-specific
    /// identity fields are set afterwards.
    /// </summary>
    public LeaveCalendarEvent ToLeaveCalendarEvent(LeaveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var eventEntity =
            entry.Event ?? throw new InvalidOperationException($"Leave entry {entry.Id} has no associated event.");

        return eventEntity.Adapt<LeaveCalendarEvent>(LeaveCalendarEventMappingConfig) with
        {
            Id = $"scheduling.leave-entry.{entry.Id}",
            LeaveEntryId = entry.Id,
            LeaveSeriesId = entry.LeaveSeriesId,
            LeaveTypeId = entry.LeaveTypeId,
            LeaveDate = GetLeaveDate(eventEntity.AllDay, eventEntity.StartAtUtc, eventEntity.TimeZoneId),
            LeaveTypeName = entry.LeaveType?.Code,
            Description = entry.LeaveType?.Description,
            EventId = entry.EventId,
            UserIds = entry.Users.Select(user => user.UserId).Distinct().ToList(),
            Type = "scheduling.leave",
        };
    }

    public SchedulingLeaveEntryResponse ToSchedulingLeaveEntryResponse(LeaveEntry leaveEntry)
    {
        var userIds = leaveEntry.Users.Select(user => user.UserId).Distinct().ToList();
        var response = calendarEventMapper.ToEntryResponse(
            () => new SchedulingLeaveEntryResponse { Title = string.Empty, StartAtUtc = default },
            leaveEntry.Event!,
            userIds
        );

        return response with
        {
            Id = leaveEntry.Id,
            LeaveTypeId = leaveEntry.LeaveTypeId,
            LeaveSeriesId = leaveEntry.LeaveSeriesId,
            LeaveDate = GetLeaveDate(response.AllDay, response.StartAtUtc, response.TimeZoneId),
        };
    }

    public SchedulingLeaveSeriesResponse ToSchedulingLeaveSeriesResponse(
        LeaveSeries series,
        IReadOnlyCollection<int> eventIds,
        IReadOnlyCollection<int> leaveEntryIds
    )
    {
        var userIds = series.Users.Select(user => user.UserId).Distinct().ToList();
        var response = calendarEventMapper.ToSeriesResponse(
            () => new SchedulingLeaveSeriesResponse { Title = string.Empty, StartAtUtc = default },
            series.EventSeries!,
            userIds,
            eventIds
        );

        return response with
        {
            Id = series.Id,
            LeaveTypeId = series.LeaveTypeId,
            LeaveDate = GetLeaveDate(response.AllDay, response.StartAtUtc, response.TimeZoneId),
            LeaveEntryIds = leaveEntryIds,
        };
    }

    private DateOnly? GetLeaveDate(bool allDay, DateTimeOffset startAtUtc, string? timeZoneId) =>
        allDay
            ? DateOnly.FromDateTime(
                timeZoneService.ToLocalUnspecified(startAtUtc, timeZoneService.ResolveRequired(timeZoneId!))
            )
            : null;

    private static TypeAdapterConfig BuildLeaveCalendarEventMappingConfig()
    {
        var config = new TypeAdapterConfig();

        config
            .NewConfig<Event, LeaveCalendarEvent>()
            .Map(dest => dest.Start, src => src.StartAtUtc)
            .Map(dest => dest.End, src => src.EndAtUtc);

        return config;
    }
}
