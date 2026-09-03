using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ProposedShiftAssignmentOptionsService(
    ILogger<ProposedShiftAssignmentOptionsService> logger,
    IAssignmentService assignmentService,
    IRecurrenceExpander recurrenceExpander,
    IRecurrenceRuleValidator recurrenceRuleValidator,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : IProposedShiftAssignmentOptionsService
{
    private static readonly RecurrenceValidationOptions RecurrenceValidationOptions = new()
    {
        MaximumDuration = TimeSpan.FromDays(365),
        MaximumOccurrences = 400,
        RequireBoundedRule = true,
    };

    public async Task<ProposedShiftAssignmentOptionsResponse> GetOptionsAsync(
        ProposedShiftAssignmentOptionsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Finding assignment options for location {LocationId} with series scope {IsSeriesScope}.",
            request.LocationId,
            request.IsSeriesScope
        );

        var timeZone = timeZoneResolver.Resolve(request.TimeZoneId);
        var occurrences = ExpandOccurrences(request);
        var shiftDates = occurrences
            .SelectMany(occurrence =>
                occurrence.EndAtUtc.HasValue
                    ? new[]
                    {
                        GetLocalDate(occurrence.StartAtUtc, timeZone),
                        GetLocalDate(occurrence.EndAtUtc.Value, timeZone),
                    }
                    : [GetLocalDate(occurrence.StartAtUtc, timeZone)]
            )
            .Distinct()
            .ToList();
        var assignmentRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            shiftDates.Min(),
            shiftDates.Max(),
            timeZone
        );
        var matchingEntries = (
            await assignmentService.GetAssignmentEntriesAsync(
                new AssignmentEntryQueryParams
                {
                    LocationId = request.LocationId,
                    StartAtUtc = assignmentRange.StartAtUtc,
                    EndAtUtc = assignmentRange.EndAtUtc,
                },
                cancellationToken
            )
        ).Where(IsLinkable).Where(entry => occurrences.Any(occurrence => StartsOnShiftDate(entry, occurrence, timeZone))).ToList();
        var entryOptions = matchingEntries;

        IReadOnlyCollection<AssignmentSeriesResponse> seriesOptions = [];
        if (request.IsSeriesScope)
        {
            var series = await assignmentService.GetAssignmentSeriesAsync(
                new AssignmentSeriesQueryParams
                {
                    LocationId = request.LocationId,
                    StartAtUtc = assignmentRange.StartAtUtc,
                    EndAtUtc = assignmentRange.EndAtUtc,
                },
                cancellationToken
            );
            seriesOptions = series
                .Where(IsLinkable)
                .Where(item => matchingEntries.Any(entry => entry.AssignmentSeriesId == item.Id))
                .ToList();
        }

        var hasWarning =
            entryOptions.Any(entry => HasDateMatchWithoutTimeOverlap(entry, occurrences, timeZone))
            || seriesOptions.Any(item =>
                matchingEntries.Any(entry =>
                    entry.AssignmentSeriesId == item.Id && HasDateMatchWithoutTimeOverlap(entry, occurrences, timeZone)
                )
            );

        logger.LogDebug(
            "Found {EntryOptionCount} assignment entry options and {SeriesOptionCount} assignment series options for location {LocationId}.",
            entryOptions.Count,
            seriesOptions.Count,
            request.LocationId
        );

        return new ProposedShiftAssignmentOptionsResponse
        {
            EntryOptions = entryOptions,
            SeriesOptions = seriesOptions,
            HasSameDayNonOverlappingAssignments = hasWarning,
        };
    }

    private IReadOnlyCollection<SeriesEntry> ExpandOccurrences(ProposedShiftAssignmentOptionsRequest request)
    {
        if (!request.IsSeriesScope || string.IsNullOrWhiteSpace(request.RecurrenceRule))
        {
            return [new SeriesEntry { StartAtUtc = request.StartAtUtc, EndAtUtc = request.EndAtUtc }];
        }

        var validationResult = recurrenceRuleValidator.Validate(
            request.RecurrenceRule,
            request.StartAtUtc,
            request.EndAtUtc,
            request.TimeZoneId,
            RecurrenceValidationOptions
        );
        if (!validationResult.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationResult.Errors));

        return recurrenceExpander.ExpandAllBounded(
            new EventSeries
            {
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                TimeZoneId = request.TimeZoneId,
                RecurrenceRule = request.RecurrenceRule,
            },
            RecurrenceValidationOptions.MaximumOccurrences
        );
    }

    private DateOnly GetLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(instant, timeZone));

    private bool StartsOnShiftDate(AssignmentEntryResponse entry, SeriesEntry occurrence, TimeZoneInfo timeZone)
    {
        if (!entry.StartAtUtc.HasValue)
            return false;

        var assignmentDate = GetLocalDate(entry.StartAtUtc.Value, timeZone);
        return assignmentDate == GetLocalDate(occurrence.StartAtUtc, timeZone)
            || (occurrence.EndAtUtc.HasValue && assignmentDate == GetLocalDate(occurrence.EndAtUtc.Value, timeZone));
    }

    private bool HasDateMatchWithoutTimeOverlap(
        AssignmentEntryResponse entry,
        IReadOnlyCollection<SeriesEntry> occurrences,
        TimeZoneInfo timeZone
    )
    {
        if (!entry.StartAtUtc.HasValue)
            return false;

        var dateMatches = occurrences.Where(occurrence => StartsOnShiftDate(entry, occurrence, timeZone)).ToList();
        return dateMatches.Count > 0
            && dateMatches.All(occurrence =>
                !ShiftAssignmentGuards.UtcIntervalsOverlap(
                    occurrence.StartAtUtc,
                    occurrence.EndAtUtc,
                    entry.StartAtUtc.Value,
                    entry.EndAtUtc
                )
            );
    }

    private static bool IsLinkable(AssignmentEntryResponse entry) => IsLinkable(entry.StatusTypeCode);

    private static bool IsLinkable(AssignmentSeriesResponse series) => IsLinkable(series.StatusTypeCode);

    private static bool IsLinkable(string? statusTypeCode) =>
        statusTypeCode?.Trim().ToLowerInvariant() is "draft" or "active" or "published";
}
