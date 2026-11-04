using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Guards;

public static class DuplicateLoanOutGuard
{
    private static readonly ITimeZoneService timeZoneService = new TimeZoneService();

    public static async Task EnsureValidAsync(
        UnifiedDbContext db,
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        var timeZone = timeZoneService.ResolveOrUtc(request.TimeZoneId);
        var requestDate = GetLocalDate(request.StartAtUtc, timeZone);
        var startAtUtc = timeZoneService.ToUtcInstant(requestDate.ToDateTime(TimeOnly.MinValue), timeZone);
        var dayStartUtc = startAtUtc;
        var dayEndUtc = dayStartUtc.AddDays(1);
        var exists = await GetResourceLoanOuts(db, request.UserIds)
            .AnyAsync(
                entry => entry.Event!.StartAtUtc >= dayStartUtc && entry.Event.StartAtUtc < dayEndUtc,
                cancellationToken
            );
        ThrowIfDuplicate(exists);
    }

    public static async Task EnsureValidAsync(
        UnifiedDbContext db,
        LoanOutSeriesRequest request,
        IRecurrenceExpander recurrenceExpander,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        var occurrences = recurrenceExpander.ExpandAllBounded(
            new EventSeries
            {
                StartAtUtc = request.StartAtUtc,
                EndAtUtc = request.EndAtUtc,
                TimeZoneId = request.TimeZoneId,
                RecurrenceRule = request.RecurrenceRule,
            },
            400
        );
        if (request.UserIds.Count == 0 || occurrences.Count == 0)
            return;

        var timeZone = timeZoneService.ResolveOrUtc(request.TimeZoneId);
        var recurrenceDates = occurrences
            .Select(occurrence => GetLocalDate(occurrence.StartAtUtc, timeZone))
            .ToHashSet();
        var dateRange = GetDateRange(
            occurrences.Min(occurrence => occurrence.StartAtUtc),
            occurrences.Max(occurrence => occurrence.StartAtUtc),
            request.TimeZoneId
        );
        var existingStarts = await GetResourceLoanOuts(db, request.UserIds)
            .Where(entry =>
                entry.Event!.StartAtUtc >= dateRange.StartAtUtc && entry.Event.StartAtUtc <= dateRange.EndAtUtc
            )
            .Select(entry => entry.Event!.StartAtUtc)
            .ToListAsync(cancellationToken);
        var exists = existingStarts.Any(start => recurrenceDates.Contains(GetLocalDate(start, timeZone)));
        ThrowIfDuplicate(exists);
    }

    private static IQueryable<LoanOutEntry> GetResourceLoanOuts(
        UnifiedDbContext db,
        IReadOnlyCollection<Guid> userIds
    ) =>
        db
            .LoanOutEntries.AsNoTracking()
            .Where(entry =>
                entry.Event != null
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Users.Any(user => userIds.Contains(user.UserId))
            );

    private static DateOnly GetLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(instant, timeZone));

    private static void ThrowIfDuplicate(bool exists)
    {
        if (exists)
        {
            throw new ConflictValidationException(
                new Dictionary<string, string[]>
                {
                    ["LoanOut"] = ["A selected resource already has a loan-out on one or more of these dates."],
                }
            );
        }
    }

    private static UtcDateRange GetDateRange(DateTimeOffset startAtUtc, DateTimeOffset? endAtUtc, string? timeZoneId)
    {
        var timeZone = timeZoneService.ResolveOrUtc(timeZoneId);
        var lastInstant = endAtUtc ?? startAtUtc;
        return timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            GetLocalDate(startAtUtc, timeZone),
            GetLocalDate(lastInstant, timeZone),
            timeZone
        );
    }
}
