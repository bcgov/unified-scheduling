using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.Lookup;

namespace Unified.DataMigration.Services;

public sealed class SsLeaveWriter(UnifiedDbContext db)
{
    private static readonly Dictionary<string, string> Standards = new(StringComparer.Ordinal)
    {
        ["STIIP"] = "STIIP",
        ["A/L"] = "Vacation/CTO",
        ["CTO"] = "Vacation/CTO",
        ["LWOP"] = "LWOP",
        ["Medical Under 2 Hrs"] = "Med/Dent",
        ["Medical Over 2 Hrs"] = "Med/Dent",
        ["Special Leave"] = "Special",
        ["Special"] = "Special",
        ["STIP"] = "STIIP",
        ["Annual"] = "Vacation/CTO",
    };

    // Frozen 44-row production snapshot plus the three separately approved aliases above.
    private static readonly HashSet<string> Retained = new(StringComparer.Ordinal)
    {
        "SPL Parental Leave",
        "LTD",
        "WCB",
        "Bereavement",
        "Supplemental Leave",
        "Part Time Away",
        "Union Leave",
        "Other Leave (Specify with Note)",
        "Carbine Instructor Course",
        "Pistol Instructor Course",
        "All Day Medical",
        "ATT - Advance Driver Training",
        "ATT Instructor",
        "Auxiliary - Day Off",
        "Birth of Child",
        "Cops for Cancer Ride",
        "Covid",
        "CTO carryover",
        "CTO Prior Year",
        "Day Off",
        "ESA Sick Leave",
        "Family Illness",
        "Flex Day",
        "FRO Inst.",
        "Funeral Leave",
        "ICS 100",
        "Illness",
        "JI training",
        "Loan Out - TA",
        "MOVE DAY",
        "Parental",
        "Prior year A/L",
        "PSU Initial Course",
        "PT",
        "PT AWAY",
        "SCMS",
        "SCMS Training",
    };

    public async Task TypesAsync(List<SsRow> lookups, SsWriteSession session, CancellationToken ct)
    {
        var source = lookups.Where(x => x.Int("Type") == 5).ToList();
        foreach (var row in source)
            await session.RowAsync(
                "LeaveType",
                row,
                async () =>
                {
                    var code = row.Text("Code")?.Trim();
                    if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                        throw new SsReject("InvalidLeaveCode");
                    if (source.Count(x => x.Text("Code")?.Trim() == code) != 1)
                        throw new SsReject("DuplicateTrimmedLeaveCode");
                    if (row.Int("LocationId") is not null || row.Text("SubCode") is not null)
                        throw new SsReject("ScopedLeaveType");
                    if (!Standards.ContainsKey(code) && !Retained.Contains(code))
                        throw new SsReject("UnknownLeaveType");
                    var hash = SsWriteSession.Hash(new { code });
                    if (await session.ReplayAsync("LeaveType", row, hash, true, ct))
                        return;
                    var standard = Standards.TryGetValue(code, out var canonical);
                    var targetCode = standard ? canonical! : code;
                    var targets = await db.LeaveTypes.Where(x => x.Code == targetCode).ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousLeaveType");
                    var target = targets.SingleOrDefault();
                    if (standard)
                    {
                        if (
                            target is null
                            || target.ExpiryDate is not null
                            || target.EffectiveDate > session.Now
                            || target.IsPaid != (canonical != "LWOP")
                        )
                            throw new SsReject("MissingOrInconsistentStandardLeave");
                    }
                    else if (target is not null)
                        throw new SsReject("RetainedLeaveTargetCollision");
                    var created = target is null;
                    if (target is null)
                    {
                        var description = row.Text("Description")?.Trim();
                        if (string.IsNullOrWhiteSpace(description))
                            description = code;
                        if (description.Length > 100)
                        {
                            var length = char.IsHighSurrogate(description[99]) ? 99 : 100;
                            description = description[..length].TrimEnd();
                        }
                        target = session.Stamp(
                            new LeaveType
                            {
                                Code = code,
                                Description = description,
                                IsPaid = true,
                                EffectiveDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
                                ExpiryDate = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                            }
                        );
                        db.LeaveTypes.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    await session.LinkAsync("LeaveType", row, "LeaveType", target!.Id, hash, created, ct);
                },
                ct
            );
    }

    public async Task EntriesAsync(
        List<SsRow> source,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        foreach (var row in source)
            await session.RowAsync(
                "SheriffLeave",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        session.Outcome("SheriffLeave", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    if (dates.Start == dates.End)
                        throw new SsReject("EmptyInterval");
                    var zone = SsWriteSession.Timezone(row, false);
                    var userId = Guid.Parse(await session.ResolveAsync("User", row.Guid("SheriffId")?.ToString(), ct));
                    var typeId =
                        await session.ResolveIntAsync("LeaveType", row.Int("LeaveTypeId"), ct)
                        ?? throw new SsReject("MissingLeaveType");
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            userId,
                            typeId,
                            dates,
                            zone,
                            Expiry = row.Date("ExpiryDate"),
                        }
                    );
                    if (await session.ReplayAsync("SheriffLeave", row, hash, false, ct))
                        return;
                    var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
                    var fallbackZone = zone;
                    if (fallbackZone is null && user.HomeLocationId is { } homeLocation)
                        fallbackZone = await db
                            .Locations.Where(x => x.Id == homeLocation)
                            .Select(x => x.Timezone)
                            .SingleAsync(ct);
                    var name = $"{user.FirstName} {user.LastName}".Trim();
                    var targetEvent = session.Stamp(
                        new Event
                        {
                            Title = name.Length == 0 ? "Leave" : $"{name} leave",
                            Description = row.Text("Comment"),
                            StartAtUtc = dates.Start,
                            EndAtUtc = dates.End,
                            TimeZoneId = zone,
                            AllDay = false,
                            EventTypeCode = "leave",
                            SourceModule = "leave",
                            StatusTypeCode = EventStatus(dates.Start, fallbackZone, row.Date("ExpiryDate")),
                            CancelledAt = row.Date("ExpiryDate"),
                            CancellationReason = row.Text("ExpiryReason"),
                            LocationId = user.HomeLocationId,
                        }
                    );
                    var entry = session.Stamp(new LeaveEntry { Event = targetEvent, LeaveTypeId = typeId });
                    entry.Users.Add(session.Stamp(new LeaveEntryUser { UserId = userId }));
                    db.LeaveEntries.Add(entry);
                    await db.SaveChangesAsync(ct);
                    await session.LinkAsync("SheriffLeave", row, "LeaveEntry", entry.Id, hash, true, ct);
                },
                ct
            );
    }

    public static string EventStatus(DateTimeOffset start, string? zone, DateTimeOffset? expiry)
    {
        if (expiry is not null)
            return "cancelled";
        // Null person-event timezone is stored unchanged; status needs the actual location fallback.
        // The caller must supply the resolved fallback, not a host/run-clock timezone.
        if (zone is null)
            throw new SsReject("MissingStatusTimezoneFallback");
        var date = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(start, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime
        );
        return date <= new DateOnly(2026, 12, 1) ? "active" : "draft";
    }
}
