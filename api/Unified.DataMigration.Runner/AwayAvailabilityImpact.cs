using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

internal static class AwayAvailabilityImpact
{
    internal static async Task ValidateAsync(IServiceProvider services, SsWriteSession session, CancellationToken ct)
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        var availability = services.GetRequiredService<ShiftLoanOutAvailabilityService>();
        var time = services.GetRequiredService<ITimeZoneService>();
        var resolver = services.GetRequiredService<ICalendarTimeZoneResolver>();
        if (!availability.IsEnabled)
        {
            session.Outcome(
                "AwayAvailabilityValidation",
                session.Run.Id.ToString(),
                "NeedsAction",
                "LoanOutAvailabilityDisabled"
            );
            return;
        }
        var links = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == "SheriffAwayLocation")
            .ToListAsync(ct);
        foreach (var link in links)
        {
            var entry = await db
                .LoanOutEntries.AsNoTracking()
                .Include(x => x.Event)
                .Include(x => x.Users)
                .SingleAsync(x => x.Id == int.Parse(link.TargetKey), ct);
            var e = entry.Event!;
            if (
                e.StatusTypeCode == "cancelled"
                || e.EndAtUtc is not { } end
                || e.LocationId is not { } home
                || home == entry.LoanOutLocationId
            )
                continue;
            var location = await db.Locations.AsNoTracking().SingleAsync(x => x.Id == home, ct);
            var zone = resolver.Resolve(null, location.Timezone);
            var first = DateOnly.FromDateTime(time.ToLocalUnspecified(e.StartAtUtc, zone));
            var last = DateOnly.FromDateTime(time.ToLocalUnspecified(end.AddTicks(-1), zone));
            if (last.DayNumber - first.DayNumber > 3660)
            {
                session.Outcome(
                    "AwayAvailabilityValidation",
                    link.LegacyId,
                    "NeedsAction",
                    "AwayIntervalTooLargeForBoundedImpactProbe"
                );
                continue;
            }
            decimal outsideSuppressed = 0;
            decimal futureOutsideSuppressed = 0;
            var probes = 0;
            for (var day = first; day <= last; day = day.AddDays(1))
            {
                var range = time.ConvertInclusiveLocalDateRangeToUtcRange(day, day, zone);
                var insideStart = e.StartAtUtc > range.StartAtUtc ? e.StartAtUtc : range.StartAtUtc;
                var insideEnd = end < range.EndAtUtc ? end : range.EndAtUtc;
                foreach (var segment in new[] { (range.StartAtUtc, insideStart), (insideEnd, range.EndAtUtc) })
                {
                    if (segment.Item2 <= segment.Item1)
                        continue;
                    var assigned = await availability.GetAssignableUserIdsByOccurrenceAsync(
                        entry.Users.Select(x => x.UserId).ToArray(),
                        [new ShiftAssignmentLoanOutContext(segment.Item1, segment.Item2)],
                        home,
                        zone.Id,
                        ct
                    );
                    var blocked = entry.Users.Count - assigned.Single().Count;
                    var minutes = (decimal)(segment.Item2 - segment.Item1).TotalMinutes * blocked;
                    outsideSuppressed += minutes;
                    if (day >= new DateOnly(2026, 12, 1))
                        futureOutsideSuppressed += minutes;
                    probes++;
                }
            }
            session.Outcome(
                "AwayAvailabilityValidation",
                link.LegacyId,
                futureOutsideSuppressed > 0 ? "NeedsAction"
                    : outsideSuppressed > 0 ? "MigratedWithDifference"
                    : "Migrated",
                futureOutsideSuppressed > 0 ? "UnapprovedFutureAvailabilityLossOutsideSsInterval"
                    : outsideSuppressed > 0 ? "HistoricalAvailabilityLossOutsideSsInterval"
                    : "NoOutsideIntervalLossObserved"
            );
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new
                    {
                        Query = "ShiftLoanOutAvailabilityService.OutsideSourceInterval",
                        SourceAwayId = link.LegacyId,
                        ProbeCount = probes,
                        OutsideSourceSuppressedUserMinutes = outsideSuppressed,
                        FutureOutsideSourceSuppressedUserMinutes = futureOutsideSuppressed,
                        IsExecutedSsAvailabilityOracle = false,
                    }
                )
            );
        }
    }
}
