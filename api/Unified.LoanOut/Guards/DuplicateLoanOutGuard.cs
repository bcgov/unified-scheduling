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
        int? existingEntryId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        var timeZone = timeZoneService.ResolveOrUtc(request.TimeZoneId);
        var requestDate = GetLocalDate(request.StartAtUtc, timeZone);
        var dateRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(requestDate, requestDate, timeZone);
        var exists = await GetResourceLoanOuts(db, request.UserIds)
            .AnyAsync(
                entry =>
                    entry.Event!.StartAtUtc >= dateRange.StartAtUtc
                    && entry.Event.StartAtUtc < dateRange.EndAtUtc
                    && entry.Users.Any(user =>
                        request.UserIds.Contains(user.UserId)
                        && (
                            existingEntryId == null
                            || !(entry.Id == existingEntryId && request.UserIds.Contains(user.UserId))
                        )
                    ),
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

        if (request.UserIds.Count == 0)
            return;

        var timeZone = timeZoneService.ResolveOrUtc(request.TimeZoneId);
        var occurrenceDates = recurrenceExpander
            .ExpandAllBounded(
                new EventSeries
                {
                    StartAtUtc = request.StartAtUtc,
                    EndAtUtc = request.EndAtUtc,
                    TimeZoneId = request.TimeZoneId,
                    RecurrenceRule = request.RecurrenceRule,
                },
                400
            )
            .Select(occurrence => GetLocalDate(occurrence.StartAtUtc, timeZone));
        await EnsureDateUserPairsValidAsync(
            db,
            occurrenceDates.SelectMany(date => request.UserIds.Select(userId => (date, userId))),
            timeZone,
            existingSeriesId: null,
            cancellationToken: cancellationToken
        );
    }

    public static async Task EnsureOccurrencesValidAsync(
        UnifiedDbContext db,
        IReadOnlyCollection<LoanOutEntry> entries,
        string? timeZoneId,
        int existingSeriesId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        cancellationToken.ThrowIfCancellationRequested();
        var timeZone = timeZoneService.ResolveOrUtc(timeZoneId);
        var pairs = entries
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .SelectMany(entry =>
                entry
                    .Users.Select(user => user.UserId)
                    .Distinct()
                    .Select(userId => (GetLocalDate(entry.Event!.StartAtUtc, timeZone), userId))
            );
        await EnsureDateUserPairsValidAsync(db, pairs, timeZone, existingSeriesId, cancellationToken);
    }

    private static async Task EnsureDateUserPairsValidAsync(
        UnifiedDbContext db,
        IEnumerable<(DateOnly Date, Guid UserId)> pairs,
        TimeZoneInfo timeZone,
        int? existingSeriesId,
        CancellationToken cancellationToken
    )
    {
        var proposedPairs = new HashSet<(DateOnly Date, Guid UserId)>();
        foreach (var pair in pairs)
            ThrowIfDuplicate(!proposedPairs.Add(pair));
        if (proposedPairs.Count == 0)
            return;

        var userIds = proposedPairs.Select(pair => pair.UserId).Distinct().ToList();
        var dateRange = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            proposedPairs.Min(pair => pair.Date),
            proposedPairs.Max(pair => pair.Date),
            timeZone
        );
        var otherUsers = await GetResourceLoanOuts(db, userIds)
            .Where(entry =>
                (existingSeriesId == null || entry.LoanOutSeriesId != existingSeriesId)
                && entry.Event!.StartAtUtc >= dateRange.StartAtUtc
                && entry.Event.StartAtUtc < dateRange.EndAtUtc
            )
            .SelectMany(entry => entry.Users, (entry, user) => new { entry.Event!.StartAtUtc, user.UserId })
            .Where(row => userIds.Contains(row.UserId))
            .ToListAsync(cancellationToken);
        ThrowIfDuplicate(
            otherUsers.Any(row => proposedPairs.Contains((GetLocalDate(row.StartAtUtc, timeZone), row.UserId)))
        );
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
            )
            .AsQueryable();

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
}
