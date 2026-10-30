using System.Globalization;
using Mapster;
using Unified.Calendar.Extensions;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Mappings;

public sealed class SchedulingLoanOutEventMapper(
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
)
{
    private static readonly TypeAdapterConfig MappingConfig = BuildMappingConfig();

    public ShiftEntryRequest ToShiftEntryRequest(ShiftEntry entry, int locationId, IReadOnlyCollection<Guid> userIds)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Event);

        return entry.Adapt<ShiftEntryRequest>(MappingConfig) with
        {
            LocationId = locationId,
            UserIds = userIds,
            ShiftSeriesId = null,
            SeriesStartAtUtc = null,
            SeriesEndAtUtc = null,
        };
    }

    public ShiftSeriesRequest ToShiftSeriesRequest(
        ShiftSeries source,
        IReadOnlyCollection<ShiftEntry> occurrences,
        int locationId,
        IReadOnlyCollection<Guid> userIds,
        string? recurrenceRuleOverride = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.EventSeries);
        ArgumentNullException.ThrowIfNull(occurrences);
        if (occurrences.Count == 0)
            throw new ArgumentException("At least one shift occurrence is required.", nameof(occurrences));

        var sourceSeries = source.EventSeries;
        var orderedOccurrences = occurrences.OrderBy(occurrence => occurrence.Event?.StartAtUtc).ToList();
        if (orderedOccurrences.Any(occurrence => occurrence.Event is null))
            throw new ArgumentException("Shift occurrences must have associated events.", nameof(occurrences));
        var firstOccurrence = orderedOccurrences[0];
        var timeZone = timeZoneResolver.Resolve(sourceSeries.TimeZoneId);
        var localRange = sourceSeries.ToLocalTimeRange(timeZoneService, timeZone);
        var firstOccurrenceLocalStart = timeZoneService.ToLocalUnspecified(firstOccurrence.Event!.StartAtUtc, timeZone);
        var recurrenceRule = recurrenceRuleOverride ?? sourceSeries.RecurrenceRule;
        if (string.IsNullOrWhiteSpace(recurrenceRule))
            throw new ArgumentException("A recurring shift series must have a recurrence rule.", nameof(source));

        return new ShiftSeriesRequest
        {
            Title = sourceSeries.Title,
            Description = sourceSeries.Description,
            Notes = sourceSeries.Notes,
            Color = sourceSeries.Color,
            StartAtUtc = firstOccurrence.Event!.StartAtUtc,
            EndAtUtc = localRange.Duration.HasValue
                ? timeZoneService.ToUtcInstant(firstOccurrenceLocalStart + localRange.Duration.Value, timeZone)
                : null,
            TimeZoneId = sourceSeries.TimeZoneId,
            AllDay = sourceSeries.AllDay,
            LocationId = locationId,
            UserIds = userIds,
            RecurrenceRule = LimitRecurrenceRule(recurrenceRule, orderedOccurrences.Count),
        };
    }

    private static string LimitRecurrenceRule(string recurrenceRule, int occurrenceCount)
    {
        var normalizedRule = recurrenceRule.Trim();
        if (normalizedRule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            normalizedRule = normalizedRule["RRULE:".Length..].Trim();

        var parts = normalizedRule
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part =>
                !part.StartsWith("COUNT=", StringComparison.OrdinalIgnoreCase)
                && !part.StartsWith("UNTIL=", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        // COUNT avoids translating the final occurrence through iCal.NET's
        parts.Add($"COUNT={occurrenceCount.ToString(CultureInfo.InvariantCulture)}");
        return string.Join(';', parts);
    }

    private static TypeAdapterConfig BuildMappingConfig()
    {
        var config = new TypeAdapterConfig();
        config
            .NewConfig<ShiftEntry, ShiftEntryRequest>()
            .Map(destination => destination.Title, source => source.Event!.Title)
            .Map(destination => destination.Description, source => source.Event!.Description)
            .Map(destination => destination.Notes, source => source.Event!.Notes)
            .Map(destination => destination.Color, source => source.Event!.Color)
            .Map(destination => destination.StartAtUtc, source => source.Event!.StartAtUtc)
            .Map(destination => destination.EndAtUtc, source => source.Event!.EndAtUtc)
            .Ignore(destination => destination.SeriesStartAtUtc)
            .Ignore(destination => destination.SeriesEndAtUtc)
            .Map(destination => destination.TimeZoneId, source => source.Event!.TimeZoneId)
            .Map(destination => destination.AllDay, source => source.Event!.AllDay)
            .Ignore(destination => destination.ShiftSeriesId)
            .Ignore(destination => destination.AssignmentEntryLinks);

        return config;
    }
}
