using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.DataMigration.Services;

/// <summary>G10 links existing migrated duties only; it never materializes or regenerates events.</summary>
public sealed class SsAssignmentSeriesWriter(UnifiedDbContext db)
{
    private static readonly DateOnly Cutover = new(2026, 12, 1);
    private static readonly string[] Days =
    [
        "Sunday",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
    ];
    private static readonly string[] DayCodes = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];

    public async Task WriteAsync(Dictionary<string, List<SsRow>> rows, SsWriteSession s, CancellationToken ct) =>
        await WriteAsync(rows, s, ct, null);

    internal async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        CancellationToken ct,
        List<SsSchedulingProposal>? proposals
    )
    {
        foreach (var assignment in rows["Assignment"])
        {
            var sourceDuties = rows["Duty"]
                .Where(x =>
                    x.Int("AssignmentId")?.ToString(CultureInfo.InvariantCulture) == assignment.Id
                    && x.Date("ExpiryDate") is null
                )
                .OrderBy(x => x.Date("StartDate"))
                .ThenBy(x => x.Int("Id"))
                .ToList();
            if (sourceDuties.Count == 0)
                continue;
            var key = $"g10:assignment:{assignment.Id}";
            var synthetic = new SsRow(
                new Dictionary<string, object?>
                {
                    ["Id"] = key,
                    ["CreatedOn"] = assignment.Date("CreatedOn")?.UtcDateTime,
                    ["UpdatedOn"] = sourceDuties
                        .Select(x => x.Date("UpdatedOn") ?? x.Date("CreatedOn"))
                        .Append(assignment.Date("UpdatedOn") ?? assignment.Date("CreatedOn"))
                        .Max()
                        ?.UtcDateTime,
                }
            );
            await s.RowAsync(
                "AssignmentSeries",
                synthetic,
                async () =>
                {
                    var zone = SsWriteSession.Timezone(assignment, true)!;
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
                    DateTime Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, tz).DateTime;
                    var startTime = assignment.Time("Start");
                    var endTime = assignment.Time("End");
                    if (startTime is null || endTime is null || endTime <= startTime || !Days.Any(assignment.Bool))
                    {
                        s.Outcome("AssignmentSeries", key, "Excluded", "StandaloneUnsupportedPattern");
                        return;
                    }
                    // Unified's draft-series regeneration explicitly rejects any Active child.
                    // Use the approved next-day fallback; cutover-day Active entries remain standalone.
                    var candidates = sourceDuties
                        .Where(x =>
                            x.Date("StartDate") is { } start
                            && x.Date("EndDate") is { } end
                            && DateOnly.FromDateTime(Local(start)) > Cutover
                            && Local(start).TimeOfDay == startTime
                            && Local(end).TimeOfDay == endTime
                            && Local(start).Date == Local(end).Date
                            && assignment.Bool(Days[(int)Local(start).DayOfWeek])
                            && (
                                assignment.Date("AdhocStartDate") is not { } adhocStart
                                || Local(start).Date >= Local(adhocStart).Date
                            )
                            && (
                                assignment.Date("AdhocEndDate") is not { } adhocEnd
                                || Local(start).Date <= Local(adhocEnd).Date
                            )
                        )
                        .ToList();
                    if (candidates.Count < 2)
                    {
                        s.Outcome("AssignmentSeries", key, "Excluded", "StandaloneInsufficientPatternOccurrences");
                        return;
                    }
                    var byDay = candidates
                        .GroupBy(x => DateOnly.FromDateTime(Local(x.Date("StartDate")!.Value)))
                        .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Int("Id")).First());
                    var first = byDay.Keys.Min();
                    var last = byDay.Keys.Max();
                    var expected = new List<SsRow>();
                    for (var day = first; day <= last; day = day.AddDays(1))
                    {
                        if (!assignment.Bool(Days[(int)day.DayOfWeek]))
                            continue;
                        if (!byDay.TryGetValue(day, out var duty))
                        {
                            s.Outcome("AssignmentSeries", key, "Excluded", "StandaloneMissingRruleOccurrence");
                            return;
                        }
                        // A split/multi-sheriff duty cannot be represented by one capacity-one series.
                        if (
                            rows["DutySlot"]
                                .Count(x =>
                                    x.Int("DutyId")?.ToString(CultureInfo.InvariantCulture) == duty.Id
                                    && x.Date("ExpiryDate") is null
                                ) > 1
                        )
                        {
                            s.Outcome("AssignmentSeries", key, "Excluded", "StandaloneSplitDuty");
                            return;
                        }
                        expected.Add(duty);
                    }
                    var memberTargets = new List<string>();
                    foreach (var member in expected)
                        memberTargets.Add(await s.ResolveAsync("Duty", member.Id, ct));
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            Assignment = assignment.Id,
                            Definition = await s.ResolveAsync("Assignment", assignment.Id, ct),
                            TemplateComment = expected[0].Text("Comment"),
                            ResolvedMembers = memberTargets,
                            zone,
                            startTime,
                            endTime,
                            Members = expected
                                .Select(x => new
                                {
                                    x.Id,
                                    Start = x.Date("StartDate"),
                                    End = x.Date("EndDate"),
                                })
                                .ToArray(),
                            Weekdays = Days.Select(assignment.Bool).ToArray(),
                        }
                    );
                    if (proposals is not null)
                    {
                        proposals.Add(new("AssignmentSeries", [key], hash));
                        return;
                    }
                    if (await s.ReplayAsync("AssignmentSeries", synthetic, hash, false, ct))
                        return;
                    var entries = new List<AssignmentEntry>();
                    var owners = new List<Unified.Db.Models.DataMigration.DataMigrationRecord>();
                    foreach (var duty in expected)
                    {
                        var targetId = int.Parse(
                            await s.ResolveAsync("Duty", duty.Id, ct),
                            CultureInfo.InvariantCulture
                        );
                        var owner = await db.DataMigrationRecords.SingleAsync(
                            x => x.Source == "SS" && x.EntityType == "Duty" && x.LegacyId == duty.Id,
                            ct
                        );
                        if (owner.CreatedByRunId is null)
                            throw new SsReject("SeriesCannotChangeLinkedOnlyEntry");
                        owners.Add(owner);
                        var entry = await db
                            .AssignmentEntries.Include(x => x.Event)
                            .SingleAsync(x => x.Id == targetId, ct);
                        if (
                            entry.AssignmentSeriesId != null
                            || entry.Event!.EventSeriesId != null
                            || entry.Event.StatusTypeCode != "draft"
                            || entry.Event.StartAtUtc != duty.Date("StartDate")
                            || entry.Event.EndAtUtc != duty.Date("EndDate")
                        )
                            throw new SsReject("SeriesEntryDriftOrExistingMembership");
                        entries.Add(entry);
                    }
                    var template = entries[0];
                    // A valid source pattern need not be representable by one
                    // constant-capacity/classification series. Preserve its real
                    // standalone entries, BEFORE composing or adding any series.
                    // Never downgrade external target edits into this fallback.
                    if (
                        entries.Any(entry =>
                            entry.AssignmentDefinitionId != template.AssignmentDefinitionId
                            || entry.Capacity != template.Capacity
                            || entry.CategoryId != template.CategoryId
                            || entry.SubCategoryId != template.SubCategoryId
                            || entry.Event!.LocationId != template.Event!.LocationId
                        )
                    )
                    {
                        await s.GuardGraphAsync(owners, ct);
                        s.Outcome("AssignmentSeries", key, "Excluded", "StandaloneInconsistentMember");
                        return;
                    }
                    var targetSeries = s.Stamp(
                        new EventSeries
                        {
                            Title = template.Event!.Title,
                            Description = template.Event.Description,
                            Color = template.Event.Color,
                            LocationId = template.Event.LocationId,
                            TimeZoneId = zone,
                            StartAtUtc = template.Event.StartAtUtc,
                            EndAtUtc = template.Event.EndAtUtc,
                            AllDay = false,
                            EventTypeCode = "assignment",
                            StatusTypeCode = "draft",
                            RecurrenceRule =
                                $"FREQ=WEEKLY;BYDAY={string.Join(",", Enumerable.Range(0, 7).Where(i => assignment.Bool(Days[i])).Select(i => DayCodes[i]))};COUNT={expected.Count}",
                        }
                    );
                    var time = new TimeZoneService();
                    var resolver = new CalendarTimeZoneResolver(
                        Microsoft.Extensions.Options.Options.Create(
                            new CalendarDateTimeOptions { DefaultTimeZoneId = zone }
                        ),
                        time
                    );
                    var expansion = new IcalNetRecurrenceExpander(time, resolver).ExpandAllBounded(
                        targetSeries,
                        expected.Count
                    );
                    if (
                        expansion.Count != entries.Count
                        || expansion.Any(x =>
                            !entries.Any(e => e.Event!.StartAtUtc == x.StartAtUtc && e.Event.EndAtUtc == x.EndAtUtc)
                        )
                    )
                        throw new SsReject("ActualRruleExpansionDoesNotMatchSource");
                    var series = s.Stamp(
                        new AssignmentSeries
                        {
                            EventSeries = targetSeries,
                            AssignmentDefinitionId = template.AssignmentDefinitionId,
                            Capacity = template.Capacity,
                            CategoryId = template.CategoryId,
                            SubCategoryId = template.SubCategoryId,
                        }
                    );
                    await s.ComposeAsync(
                        "Duty",
                        expected.Select(x => x.Id).ToArray(),
                        async () =>
                        {
                            foreach (var entry in entries)
                            {
                                if (
                                    entry.AssignmentDefinitionId != series.AssignmentDefinitionId
                                    || entry.Capacity != series.Capacity
                                    || entry.CategoryId != series.CategoryId
                                    || entry.SubCategoryId != series.SubCategoryId
                                    || entry.Event!.LocationId != targetSeries.LocationId
                                )
                                    throw new SsReject("InconsistentSeriesMember");
                                entry.AssignmentSeries = series;
                                entry.Event!.EventSeries = targetSeries;
                                entry.Event.SeriesStartAtUtc = entry.Event.StartAtUtc;
                                entry.Event.SeriesEndAtUtc = entry.Event.EndAtUtc;
                            }
                            db.AssignmentSeries.Add(series);
                            await db.SaveChangesAsync(ct);
                        },
                        ct
                    );
                    await s.LinkAsync(
                        "AssignmentSeries",
                        synthetic,
                        "AssignmentSeries",
                        series.Id,
                        hash,
                        true,
                        ct,
                        "MigratedWithDifference",
                        "G10DraftSeriesCutoverActiveStandalone"
                    );
                },
                ct
            );
        }
    }
}
