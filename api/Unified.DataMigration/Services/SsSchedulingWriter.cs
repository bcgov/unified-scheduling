using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.DataMigration.Services;

/// <summary>Tracked imports only: never invoke populate/regenerate or bypass target database constraints.</summary>
public sealed class SsSchedulingWriter(UnifiedDbContext db)
{
    public static readonly string[] Tables = ["LookupCode", "Assignment", "Duty", "DutySlot", "Shift"];

    private sealed record ShiftCandidate(
        SsRow Row,
        Guid User,
        int Location,
        string Zone,
        DateOnly Day,
        DateTimeOffset Start,
        DateTimeOffset End,
        bool Cancelled
    );

    public async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        rows = SwapReversedDates(rows);
        await RoomsAsync(rows, session, ct);
        await ClassificationsAsync(rows, session, ct);
        // A later cutoff must not implicitly remove a member applied by an
        // earlier backfill. Reuse that same complete selected universe for
        // proposals, replay and normal writes; do not admit never-applied older
        // rows by globally lowering the boundary against the raw snapshot.
        var retained = (
            await db
                .DataMigrationRecords.AsNoTracking()
                .Where(x => x.Source == "SS" && x.EntityType == "SchedulingMember")
                .Select(x => x.LegacyId)
                .ToListAsync(ct)
        ).ToHashSet();
        var selected = rows.ToDictionary(
            x => x.Key,
            x =>
                x.Key is "Duty" or "DutySlot" or "Shift"
                    ? x.Value.Where(r => r.Date("EndDate") >= boundary || retained.Contains($"{x.Key}:{r.Id}")).ToList()
                    : x.Value
        );
        var selectedRows = selected
            .Where(x => x.Key is "Duty" or "DutySlot" or "Shift")
            .ToDictionary(x => x.Key, x => x.Value.ToHashSet<SsRow>(ReferenceEqualityComparer.Instance));
        foreach (var table in rows.Where(x => x.Key is "Duty" or "DutySlot" or "Shift"))
        foreach (var row in table.Value.Where(x => !selectedRows[table.Key].Contains(x)))
            session.Outcome(table.Key, row.Id, "Excluded", "BeforeHistoryBoundary");
        boundary = selected
            .Where(x => x.Key is "Duty" or "DutySlot" or "Shift")
            .SelectMany(x => x.Value)
            .Select(x => x.Date("EndDate") ?? boundary)
            .Append(boundary)
            .Min();
        var proposals = new List<SsSchedulingProposal>();
        await session.PlanGraphAsync(
            async () =>
            {
                await DefinitionsAsync(selected, session, boundary, ct, proposals);
                await ShiftsAsync(selected["Shift"], session, boundary, ct, proposals);
                await DutiesAsync(selected, session, boundary, ct, proposals);
                await new SsAssignmentSeriesWriter(db).WriteAsync(selected, session, ct, proposals);
            },
            ct
        );
        var graphs = new SsSchedulingGraphReplay(db);
        var remaining = await graphs.ReplayAsync(
            selected,
            session,
            boundary,
            subset => WriteGraphAsync(subset, session, boundary, ct),
            ct,
            proposals
        );
        await WriteGraphAsync(remaining, session, boundary, ct);
        await graphs.CaptureAsync(selected, session, boundary, ct);
    }

    // Legacy SS has a few rows whose StartDate/EndDate were entered backwards; the interval
    // itself is valid, so swap them instead of rejecting the row and cascading to its duty.
    internal static Dictionary<string, List<SsRow>> SwapReversedDates(Dictionary<string, List<SsRow>> rows) =>
        rows.ToDictionary(
            x => x.Key,
            x =>
                x.Key is "Duty" or "DutySlot" or "Shift"
                    ? x.Value.Select(r => r.WithReversedDatesSwapped()).ToList()
                    : x.Value
        );

    private async Task WriteGraphAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        await DefinitionsAsync(rows, session, boundary, ct);
        await ShiftsAsync(rows["Shift"], session, boundary, ct);
        await DutiesAsync(rows, session, boundary, ct);
        await new SsAssignmentSeriesWriter(db).WriteAsync(rows, session, ct);
    }

    private async Task ClassificationsAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        CancellationToken ct
    )
    {
        foreach (var row in rows["LookupCode"].Where(x => x.Int("Type") is >= 1 and <= 4))
            await s.RowAsync(
                "AssignmentClassification",
                row,
                async () =>
                {
                    if (!rows["Assignment"].Any(x => x.Int("LookupCodeId") == row.Int("Id")))
                    {
                        s.Outcome("AssignmentClassification", row.Id, "Excluded", "UnusedClassificationCode");
                        return;
                    }
                    if (row.Text("Code") is "Test" or "Leave Waitlist")
                    {
                        s.Outcome("AssignmentClassification", row.Id, "Excluded", "ApprovedTestOrLeaveWaitlist");
                        return;
                    }
                    var (category, subcategory, assumed) = Classification(row);
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            Type = row.Int("Type"),
                            Code = row.Text("Code"),
                            category,
                            subcategory,
                        }
                    );
                    if (await s.ReplayAsync("AssignmentClassification", row, hash, true, ct))
                        return;
                    if (
                        !await db.SubCategories.AnyAsync(
                            x =>
                                x.Id == subcategory
                                && x.CategoryId == category
                                && x.Category != null
                                && x.Category.GroupId != 3,
                            ct
                        )
                    )
                        throw new SsReject("MissingStatsClassification");
                    await s.LinkAsync(
                        "AssignmentClassification",
                        row,
                        "SubCategory",
                        subcategory,
                        hash,
                        false,
                        ct,
                        assumed ? "MigratedWithDifference" : "Migrated",
                        assumed ? "StatsAssumptionC1S3" : null
                    );
                },
                ct
            );
    }

    private async Task RoomsAsync(Dictionary<string, List<SsRow>> rows, SsWriteSession s, CancellationToken ct)
    {
        foreach (var row in rows["LookupCode"].Where(x => x.Int("Type") == 0))
            await s.RowAsync(
                "CourtRoom",
                row,
                async () =>
                {
                    var code = row.Text("Code") ?? throw new SsReject("MissingRoomCode");
                    if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                        throw new SsReject("InvalidRoomCode");
                    var location = await s.ResolveIntAsync("Location", row.Int("LocationId"), ct);
                    var hash = SsWriteSession.Hash(new { code, location });
                    if (await s.ReplayAsync("CourtRoom", row, hash, true, ct))
                        return;
                    var targets = await db
                        .CourtRooms.Where(x => x.Room == code && x.LocationId == location)
                        .ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousRoom");
                    var target = targets.SingleOrDefault();
                    if (
                        target is null
                        && row.Date("ExpiryDate") is not null
                        && !rows["Assignment"].Any(x => x.Int("LookupCodeId") == row.Int("Id"))
                    )
                    {
                        s.Outcome("CourtRoom", row.Id, "Excluded", "ExpiredUnreferencedRoom");
                        return;
                    }
                    var created = target is null;
                    if (target is null)
                    {
                        target = s.Stamp(
                            new CourtRoom
                            {
                                Room = code,
                                LocationId = location,
                                EffectiveDate = row.Date("EffectiveDate"),
                                ExpiryDate = row.Date("ExpiryDate"),
                            }
                        );
                        db.CourtRooms.Add(target);
                        await db.SaveChangesAsync(ct);
                    }
                    await s.LinkAsync("CourtRoom", row, "CourtRoom", target.Id, hash, created, ct);
                },
                ct
            );
    }

    private static bool Excluded(SsRow assignment, Dictionary<string, SsRow> lookup) =>
        assignment.Int("LookupCodeId") is { } id
        && lookup.TryGetValue(id.ToString(), out var code)
        && code.Text("Code") is "Test" or "Leave Waitlist";

    private async Task DefinitionsAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        DateTimeOffset boundary,
        CancellationToken ct,
        List<SsSchedulingProposal>? proposals = null
    )
    {
        var lookup = rows["LookupCode"].ToDictionary(x => x.Id);
        var prepared = new List<(SsRow Row, SsRow Code, int Location, string Name, string Normalized)>();
        // Built on first use, inside the row, so a malformed snapshot fails the same row as a scan.
        // HashSet<int?>/ILookup keep the lifted int? equality (null matches null) of the scans.
        HashSet<int?>? referencedAssignments = null;
        foreach (var row in rows["Assignment"])
            await s.RowAsync(
                "Assignment",
                row,
                async () =>
                {
                    referencedAssignments ??= rows["Duty"]
                        .Where(x => x.Date("EndDate") >= boundary)
                        .Select(x => x.Int("AssignmentId"))
                        .ToHashSet();
                    var referenced = referencedAssignments.Contains(row.Int("Id"));
                    if (row.Date("ExpiryDate") is not null && !referenced)
                    {
                        s.Outcome("Assignment", row.Id, "Excluded", "ExpiredUnreferencedDefinition");
                        return;
                    }
                    if (!lookup.TryGetValue(row.Int("LookupCodeId")?.ToString() ?? "", out var code))
                        throw new SsReject("MissingAssignmentLookup");
                    if (Excluded(row, lookup))
                    {
                        s.Outcome("Assignment", row.Id, "Excluded", "ApprovedTestOrLeaveWaitlist");
                        return;
                    }
                    SsWriteSession.Timezone(row, true);
                    var location =
                        await s.ResolveIntAsync("Location", row.Int("LocationId"), ct)
                        ?? throw new SsReject("MissingAssignmentLocation");
                    if (code.Int("Type") == 0)
                        await s.ResolveIntAsync("CourtRoom", code.Int("Id"), ct);
                    else
                        await s.ResolveIntAsync("AssignmentClassification", code.Int("Id"), ct);
                    if (code.Int("LocationId") is { } sourceLocation)
                        await s.ResolveIntAsync("Location", sourceLocation, ct);
                    var codeText = code.Text("Code") ?? throw new SsReject("MissingAssignmentCode");
                    var name = string.IsNullOrWhiteSpace(row.Text("Name"))
                        ? codeText
                        : $"{codeText} ({row.Text("Name")})";
                    name = name.Trim();
                    if (name.Length is 0 or > 100)
                        throw new SsReject("DefinitionNameTooLong");
                    prepared.Add((row, code, location, name, name.ToUpperInvariant()));
                },
                ct
            );
        var groups = prepared.GroupBy(x => (x.Location, Type: x.Code.Int("Type"), x.Normalized)).ToList();
        // Group keys are distinct, so another group with this (Location, Normalized) has another Type.
        var namesakes = groups
            .GroupBy(x => (x.Key.Location, x.Key.Normalized))
            .ToDictionary(x => x.Key, x => x.Count());
        // Duties by AssignmentId with their snapshot positions, so per-group scans keep snapshot order.
        ILookup<int?, (SsRow Duty, int Position)>? dutiesByAssignment = null;
        foreach (var group in groups)
        {
            var members = group.OrderBy(x => x.Row.Int("Id")).ToList();
            await s.RowAsync(
                "Assignment",
                members[0].Row,
                async () =>
                {
                    var chosen = members
                        .OrderByDescending(x => x.Row.Date("ExpiryDate") is null)
                        .ThenByDescending(x => x.Row.Date("UpdatedOn") ?? x.Row.Date("CreatedOn"))
                        .ThenByDescending(x => x.Row.Int("Id"))
                        .First();
                    var name = chosen.Name;
                    if (namesakes[(group.Key.Location, group.Key.Normalized)] > 1)
                        name += $" [Type {group.Key.Type}]";
                    if (name.Length > 100)
                        throw new SsReject("CrossTypeDefinitionNameTooLong");
                    var fingerprint = SsWriteSession.Hash(
                        members
                            .Select(x => new
                            {
                                x.Row.Id,
                                ProposedName = x.Name,
                                Type = x.Code.Int("Type"),
                                Code = x.Code.Text("Code"),
                                x.Location,
                                Expiry = x.Row.Date("ExpiryDate"),
                                AdhocEnd = x.Row.Date("AdhocEndDate"),
                                DefaultStart = x.Row.Time("Start"),
                                DefaultEnd = x.Row.Time("End"),
                                Comment = x.Row.Text("Comment"),
                                Changed = x.Row.Date("UpdatedOn") ?? x.Row.Date("CreatedOn"),
                            })
                            .ToArray()
                    );
                    var existing = await db
                        .DataMigrationRecords.Where(x =>
                            x.Source == "SS"
                            && x.EntityType == "Assignment"
                            && members.Select(y => y.Row.Id).Contains(x.LegacyId)
                        )
                        .ToListAsync(ct);
                    var (category, subcategory, assumed) = Classification(chosen.Code);
                    if (
                        !await db.SubCategories.AnyAsync(
                            x =>
                                x.Id == subcategory
                                && x.CategoryId == category
                                && x.Category != null
                                && x.Category.GroupId != 3,
                            ct
                        )
                    )
                        throw new SsReject("MissingStatsClassification");
                    var existingIds = existing.Select(x => int.Parse(x.TargetKey)).ToArray();
                    if (
                        await db.AssignmentDefinitions.AnyAsync(
                            x =>
                                x.LocationId == group.Key.Location
                                && x.NormalizedName == name.ToUpperInvariant()
                                && !existingIds.Contains(x.Id),
                            ct
                        )
                    )
                        throw new SsReject("ExistingDefinitionCollision");
                    DateTimeOffset? ValidTo(SsRow row)
                    {
                        var expiry = row.Date("ExpiryDate");
                        var end = row.Date("AdhocEndDate")?.AddDays(1);
                        var min =
                            expiry is null ? end
                            : end is null ? expiry
                            : expiry < end ? expiry
                            : end;
                        return min is null ? null : new DateTimeOffset(min.Value.UtcDateTime.Date, TimeSpan.Zero);
                    }
                    DateTimeOffset? validTo = members.Any(x => ValidTo(x.Row) is null)
                        ? null
                        : members.Max(x => ValidTo(x.Row));
                    dutiesByAssignment ??= rows["Duty"]
                        .Select((duty, position) => (Duty: duty, Position: position))
                        .ToLookup(x => x.Duty.Int("AssignmentId"));
                    var memberDuties = members
                        .Select(y => y.Row.Int("Id"))
                        .Distinct()
                        .SelectMany(id => dutiesByAssignment[id])
                        .OrderBy(x => x.Position)
                        .Select(x => x.Duty)
                        .ToList();
                    var dutyEnd = memberDuties
                        .Where(x => x.Date("ExpiryDate") is null && x.Date("EndDate") >= boundary)
                        .Select(x => x.Date("EndDate"))
                        .Max();
                    var widened = validTo is not null && dutyEnd >= validTo;
                    if (widened)
                        validTo = new DateTimeOffset(dutyEnd!.Value.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
                    var target = s.Stamp(
                        new AssignmentDefinition
                        {
                            LocationId = group.Key.Location,
                            Name = name,
                            NormalizedName = name.ToUpperInvariant(),
                            Description = chosen.Row.Text("Comment"),
                            CategoryId = category,
                            SubCategoryId = subcategory,
                            EffectiveDateUtc = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
                            ExpiryDateUtc = validTo,
                            DefaultCapacity = 1,
                            Color = chosen.Code.Int("Type") switch
                            {
                                0 or 1 => "#189fd4",
                                2 => "#A22BB9",
                                3 => "#ffb007",
                                _ => "#7a4528",
                            },
                        }
                    );
                    // A singleton keeps its definition's own defaults, not observed duty times.
                    // Merged definitions use the most common duty interval instead.
                    if (members.Count == 1)
                    {
                        target.DefaultStartTime = chosen.Row.Time("Start") is { } start
                            ? TimeOnly.FromTimeSpan(start)
                            : null;
                        target.DefaultEndTime = chosen.Row.Time("End") is { } end ? TimeOnly.FromTimeSpan(end) : null;
                    }
                    else
                    {
                        var times = memberDuties
                            .Where(x => x.Date("StartDate") is not null && x.Date("EndDate") is not null)
                            .Select(x =>
                                (
                                    Start: TimeOnly.FromDateTime(
                                        TimeZoneInfo
                                            .ConvertTime(
                                                x.Date("StartDate")!.Value,
                                                TimeZoneInfo.FindSystemTimeZoneById(
                                                    x.Text("Timezone") ?? throw new SsReject("InvalidTimezone")
                                                )
                                            )
                                            .DateTime
                                    ),
                                    End: TimeOnly.FromDateTime(
                                        TimeZoneInfo
                                            .ConvertTime(
                                                x.Date("EndDate")!.Value,
                                                TimeZoneInfo.FindSystemTimeZoneById(x.Text("Timezone")!)
                                            )
                                            .DateTime
                                    )
                                )
                            )
                            .GroupBy(x => x)
                            .OrderByDescending(x => x.Count())
                            .ThenBy(x => x.Key.Start)
                            .ThenBy(x => x.Key.End)
                            .FirstOrDefault();
                        if (times is not null && times.Key.End > times.Key.Start)
                        {
                            target.DefaultStartTime = times.Key.Start;
                            target.DefaultEndTime = times.Key.End;
                        }
                    }
                    fingerprint = SsWriteSession.Hash(
                        new
                        {
                            Members = fingerprint,
                            target.DefaultStartTime,
                            target.DefaultEndTime,
                        }
                    );
                    if (proposals is not null)
                    {
                        proposals.Add(new("Assignment", members.Select(x => x.Row.Id).ToArray(), fingerprint));
                        return;
                    }
                    if (
                        await s.ReplayGroupAsync(
                            "Assignment",
                            members.Select(x => x.Row).ToList(),
                            fingerprint,
                            async key =>
                            {
                                var owned = await db.AssignmentDefinitions.SingleAsync(x => x.Id == int.Parse(key), ct);
                                owned.LocationId = target.LocationId;
                                owned.Name = target.Name;
                                owned.NormalizedName = target.NormalizedName;
                                owned.Description = target.Description;
                                owned.CategoryId = target.CategoryId;
                                owned.SubCategoryId = target.SubCategoryId;
                                owned.EffectiveDateUtc = target.EffectiveDateUtc;
                                owned.ExpiryDateUtc = target.ExpiryDateUtc;
                                owned.DefaultCapacity = target.DefaultCapacity;
                                owned.DefaultStartTime = target.DefaultStartTime;
                                owned.DefaultEndTime = target.DefaultEndTime;
                                owned.Color = target.Color;
                                owned.UpdatedOn = s.Now;
                            },
                            ct
                        )
                    )
                        return;
                    db.AssignmentDefinitions.Add(target);
                    await db.SaveChangesAsync(ct);
                    foreach (var member in members)
                        await s.LinkAsync(
                            "Assignment",
                            member.Row,
                            "AssignmentDefinition",
                            target.Id,
                            fingerprint,
                            true,
                            ct,
                            assumed || widened ? "MigratedWithDifference"
                                : members.Count > 1 ? "Merged"
                                : "Migrated",
                            assumed ? "StatsAssumptionC1S3"
                                : widened ? "DefinitionWindowWidened"
                                : null
                        );
                },
                ct
            );
            if (proposals is null)
                CompleteGroupOutcomes(s, "Assignment", members.Select(x => x.Row));
        }
    }

    // Frozen exact mapping; the fallback is the user's explicitly permitted logical Stats assumption.
    // No name/keyword heuristics and no writes to the Stats catalogue.
    private static (int Category, int Subcategory, bool Assumed) Classification(SsRow code) =>
        (code.Int("Type"), code.Text("Code")) switch
        {
            (0, _) => (1, 3, false),
            (_, "Documents") => (5, 22, false),
            (_, "Loan Out") => (12, 44, false),
            (_, "Inspector" or "Staff Sergeant" or "Supervisor") => (37, 135, false),
            (_, "Sergeant") => (37, 135, true),
            _ => (1, 3, true),
        };

    private async Task ShiftsAsync(
        List<SsRow> source,
        SsWriteSession s,
        DateTimeOffset boundary,
        CancellationToken ct,
        List<SsSchedulingProposal>? proposals = null
    )
    {
        var candidates = new List<ShiftCandidate>();
        foreach (var row in source)
            await s.RowAsync(
                "Shift",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        s.Outcome("Shift", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    if (dates.Start == dates.End)
                        throw new SsReject("EmptyInterval");
                    var zone = SsWriteSession.Timezone(row, true)!;
                    var user = Guid.Parse(await s.ResolveAsync("User", row.Guid("SheriffId")?.ToString(), ct));
                    var location =
                        await s.ResolveIntAsync("Location", row.Int("LocationId"), ct)
                        ?? throw new SsReject("MissingShiftLocation");
                    var day = DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTime(dates.Start, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime
                    );
                    candidates.Add(
                        new ShiftCandidate(
                            row,
                            user,
                            location,
                            zone,
                            day,
                            dates.Start,
                            dates.End,
                            row.Date("ExpiryDate") is not null
                        )
                    );
                },
                ct
            );
        foreach (var dayGroup in candidates.GroupBy(x => (x.User, x.Location, x.Day, x.Cancelled)))
        {
            var blocks = new List<List<ShiftCandidate>>();
            foreach (var item in dayGroup.OrderBy(x => x.Start).ThenBy(x => x.End).ThenBy(x => x.Row.Int("Id")))
            {
                if (!item.Cancelled && blocks.Count > 0 && item.Start <= blocks[^1].Max(x => x.End))
                    blocks[^1].Add(item);
                else
                    blocks.Add([item]);
            }
            if (!dayGroup.Key.Cancelled && blocks.Count > 1 && dayGroup.Key.Day > new DateOnly(2026, 12, 1))
            {
                foreach (var item in dayGroup)
                    s.Outcome("Shift", item.Row.Id, "NeedsAction", "FutureGapRequiresSourceRepair");
                continue;
            }
            foreach (var block in blocks)
            {
                var first = block[0];
                await s.RowAsync(
                    "Shift",
                    first.Row,
                    async () =>
                    {
                        if (block.Select(x => x.Zone).Distinct().Count() != 1)
                            throw new SsReject("MixedShiftTimezones");
                        var user = await db.Users.SingleAsync(x => x.Id == first.User, ct);
                        var name = $"{user.FirstName} {user.LastName}".Trim();
                        var start = block.Min(x => x.Start);
                        var end = block.Max(x => x.End);
                        var description = string.Join(
                            "\n",
                            block
                                .Select(x => x.Row.Text("Comment"))
                                .Where(x => x is not null)
                                .Distinct(StringComparer.Ordinal)
                        );
                        var hash = SsWriteSession.Hash(
                            block
                                .OrderBy(x => x.Row.Int("Id"))
                                .Select(x => new
                                {
                                    x.Row.Id,
                                    x.User,
                                    x.Location,
                                    x.Start,
                                    x.End,
                                    x.Zone,
                                    x.Cancelled,
                                    Expiry = x.Row.Date("ExpiryDate"),
                                    Title = name,
                                    Comment = x.Row.Text("Comment"),
                                    Changed = x.Row.Date("UpdatedOn") ?? x.Row.Date("CreatedOn"),
                                })
                                .ToArray()
                        );
                        if (proposals is not null)
                        {
                            proposals.Add(new("Shift", block.Select(x => x.Row.Id).ToArray(), hash));
                            return;
                        }
                        if (
                            await s.ReplayGroupAsync(
                                "Shift",
                                block.Select(x => x.Row).ToList(),
                                hash,
                                async key =>
                                {
                                    var id = int.Parse(key);
                                    var owned = await db
                                        .ShiftEntries.Include(x => x.Event)
                                        .Include(x => x.Users)
                                        .SingleAsync(x => x.Id == id, ct);
                                    if (owned.Users.Count != 1)
                                        throw new SsReject("AmbiguousOwnedShiftUsers");
                                    if (
                                        !first.Cancelled
                                        && first.Day > new DateOnly(2026, 12, 1)
                                        && await db.ShiftEntries.AnyAsync(
                                            x =>
                                                x.Id != id
                                                && x.Event != null
                                                && x.Event.LocationId == first.Location
                                                && x.Event.StatusTypeCode != "cancelled"
                                                && x.Event.StartAtUtc < end
                                                && x.Event.EndAtUtc > start
                                                && x.Users.Any(y => y.UserId == first.User),
                                            ct
                                        )
                                    )
                                        throw new SsReject("FutureShiftConflict");
                                    owned.Users.Single().UserId = first.User;
                                    owned.LunchAvailableMinutes = 60;
                                    owned.WorkedLunchMinutes = 0;
                                    owned.UpdatedOn = s.Now;
                                    var e = owned.Event!;
                                    e.Title = name.Length == 0 ? "Shift" : $"{name} shift";
                                    e.Description = description;
                                    e.StartAtUtc = start;
                                    e.EndAtUtc = end;
                                    e.TimeZoneId = first.Zone;
                                    e.LocationId = first.Location;
                                    e.AllDay = false;
                                    e.StatusTypeCode = SsLeaveWriter.EventStatus(
                                        start,
                                        first.Zone,
                                        first.Row.Date("ExpiryDate")
                                    );
                                    e.CancelledAt = first.Row.Date("ExpiryDate");
                                    e.UpdatedOn = s.Now;
                                },
                                ct
                            )
                        )
                            return;
                        if (!first.Cancelled && first.Day > new DateOnly(2026, 12, 1))
                        {
                            var tz = TimeZoneInfo.FindSystemTimeZoneById(first.Zone);
                            var from = new DateTimeOffset(
                                TimeZoneInfo.ConvertTimeToUtc(first.Day.ToDateTime(TimeOnly.MinValue), tz)
                            );
                            var to = new DateTimeOffset(
                                TimeZoneInfo.ConvertTimeToUtc(first.Day.AddDays(1).ToDateTime(TimeOnly.MinValue), tz)
                            );
                            if (
                                await db.ShiftEntries.AnyAsync(
                                    x =>
                                        x.Event != null
                                        && x.Event.LocationId == first.Location
                                        && x.Event.StatusTypeCode != "cancelled"
                                        && x.Event.StartAtUtc >= from
                                        && x.Event.StartAtUtc < to
                                        && x.Users.Any(y => y.UserId == first.User),
                                    ct
                                )
                            )
                                throw new SsReject("FutureShiftConflict");
                        }
                        var targetEvent = s.Stamp(
                            new Event
                            {
                                Title = name.Length == 0 ? "Shift" : $"{name} shift",
                                Description = description,
                                StartAtUtc = start,
                                EndAtUtc = end,
                                AllDay = false,
                                TimeZoneId = first.Zone,
                                LocationId = first.Location,
                                EventTypeCode = "shift",
                                SourceModule = "scheduling",
                                StatusTypeCode = SsLeaveWriter.EventStatus(
                                    start,
                                    first.Zone,
                                    first.Row.Date("ExpiryDate")
                                ),
                                CancelledAt = first.Row.Date("ExpiryDate"),
                            }
                        );
                        var target = s.Stamp(
                            new ShiftEntry
                            {
                                Event = targetEvent,
                                LunchAvailableMinutes = 60,
                                WorkedLunchMinutes = 0,
                            }
                        );
                        target.Users.Add(s.Stamp(new ShiftEntryUser { UserId = first.User }));
                        db.ShiftEntries.Add(target);
                        await db.SaveChangesAsync(ct);
                        foreach (var item in block)
                            await s.LinkAsync(
                                "Shift",
                                item.Row,
                                "ShiftEntry",
                                target.Id,
                                hash,
                                true,
                                ct,
                                blocks.Count > 1 || item.Row.Int("AnticipatedAssignmentId") is not null
                                        ? "MigratedWithDifference"
                                    : block.Count > 1 ? "Merged"
                                    : "Migrated",
                                blocks.Count > 1 ? "HistoricalGapBlocksSeparate"
                                    : item.Row.Int("AnticipatedAssignmentId") is not null
                                        ? "AnticipatedAssignmentOmitted"
                                    : null
                            );
                    },
                    ct
                );
                if (proposals is null)
                    CompleteGroupOutcomes(s, "Shift", block.Select(x => x.Row));
            }
        }
    }

    private async Task DutiesAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        DateTimeOffset boundary,
        CancellationToken ct,
        List<SsSchedulingProposal>? proposals = null
    )
    {
        var assignments = rows["Assignment"].ToDictionary(x => x.Id);
        var lookup = rows["LookupCode"].ToDictionary(x => x.Id);
        var selectedDuties = rows["Duty"].ToDictionary(x => x.Id);
        var slotsByDuty = rows["DutySlot"].ToLookup(x => x.Int("DutyId"));
        var shiftsBySheriffLocation = rows["Shift"].ToLookup(x => (x.Guid("SheriffId"), x.Int("LocationId")));
        var slotOutcomes = new Dictionary<string, (string Outcome, string? Reason)>();
        foreach (var row in rows["Duty"])
        {
            var relevantSlots = slotsByDuty[row.Int("Id")].ToList();
            await s.RowAsync(
                "Duty",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        s.Outcome("Duty", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var sourceAssignment =
                        row.Int("AssignmentId")?.ToString() ?? throw new SsReject("MissingAssignment");
                    if (!assignments.TryGetValue(sourceAssignment, out var assignment))
                        throw new SsReject("MissingSourceAssignment");
                    if (Excluded(assignment, lookup))
                    {
                        s.Outcome("Duty", row.Id, "Excluded", "ApprovedTestOrLeaveWaitlist");
                        return;
                    }
                    var definitionId = int.Parse(await s.ResolveAsync("Assignment", sourceAssignment, ct));
                    var definition = await db.AssignmentDefinitions.SingleAsync(x => x.Id == definitionId, ct);
                    var dates = SsWriteSession.Dates(row);
                    if (dates.Start == dates.End)
                        throw new SsReject("EmptyInterval");
                    var zone = SsWriteSession.Timezone(row, true)!;
                    var location =
                        await s.ResolveIntAsync("Location", row.Int("LocationId"), ct)
                        ?? throw new SsReject("MissingDutyLocation");
                    var live = relevantSlots
                        .Where(x => x.Date("ExpiryDate") is null && x.Date("EndDate") >= boundary)
                        .ToList();
                    foreach (var slot in live)
                    {
                        var slotDates = SsWriteSession.Dates(slot);
                        if (slotDates.Start == slotDates.End)
                            throw new SsReject("EmptySlotInterval");
                        SsWriteSession.Timezone(slot, true);
                        if (slot.Int("LocationId") is { } supplied)
                            await s.ResolveIntAsync("Location", supplied, ct);
                        if (slot.Guid("SheriffId") is { } sheriff)
                            await s.ResolveAsync("User", sheriff.ToString(), ct);
                    }
                    var overlappingSourceShifts = live.Select(slot => slot.Guid("SheriffId"))
                        .Where(id => id is not null)
                        .Distinct()
                        .SelectMany(id => shiftsBySheriffLocation[(id, row.Int("LocationId"))])
                        .Where(shift =>
                            shift.Date("EndDate") >= boundary
                            && live.Any(slot =>
                                slot.Guid("SheriffId") is { } sheriff
                                && shift.Guid("SheriffId") == sheriff
                                && shift.Int("LocationId") == row.Int("LocationId")
                                && shift.Date("StartDate") < slot.Date("EndDate")
                                && shift.Date("EndDate") > slot.Date("StartDate")
                            )
                        )
                        .ToArray();
                    var sourceShiftIds = overlappingSourceShifts.Select(x => x.Id).ToArray();
                    var resolvedShifts = await db
                        .DataMigrationRecords.AsNoTracking()
                        .Where(x => x.Source == "SS" && x.EntityType == "Shift" && sourceShiftIds.Contains(x.LegacyId))
                        .OrderBy(x => x.LegacyId)
                        .Select(x => new { x.LegacyId, x.TargetKey })
                        .ToListAsync(ct);
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            definitionId,
                            ResolvedShifts = resolvedShifts,
                            DefinitionName = definition.Name,
                            definition.Color,
                            definition.CategoryId,
                            definition.SubCategoryId,
                            dates,
                            zone,
                            location,
                            Comment = row.Text("Comment"),
                            Expiry = row.Date("ExpiryDate"),
                            // Link selection depends on the source shift graph as
                            // well as the slot. A moved/reassigned source shift must
                            // replay its affected duties even when Duty.UpdatedOn
                            // did not change. Never derive source hashes from
                            // independently edited mutable target shift values.
                            ShiftDependencies = overlappingSourceShifts
                                .OrderBy(shift => shift.Int("Id"))
                                .Select(shift => new
                                {
                                    shift.Id,
                                    Sheriff = shift.Guid("SheriffId"),
                                    Location = shift.Int("LocationId"),
                                    Start = shift.Date("StartDate"),
                                    End = shift.Date("EndDate"),
                                    Timezone = shift.Text("Timezone"),
                                    Expiry = shift.Date("ExpiryDate"),
                                })
                                .ToArray(),
                            Slots = live.OrderBy(x => x.Int("Id"))
                                .Select(x => new
                                {
                                    x.Id,
                                    User = x.Guid("SheriffId"),
                                    Start = x.Date("StartDate"),
                                    End = x.Date("EndDate"),
                                    Closed = x.Bool("IsClosed"),
                                    NotRequired = x.Bool("IsNotRequired"),
                                    NotAvailable = x.Bool("IsNotAvailable"),
                                })
                                .ToArray(),
                        }
                    );
                    if (proposals is not null)
                    {
                        proposals.Add(new("Duty", [row.Id], SsWriteSession.ProposalHash("Duty", row, hash)));
                        return;
                    }
                    if (
                        await s.ReplayAsync(
                            "Duty",
                            row,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                if (live.Count > 1)
                                    throw new SsReject("DutyMultiEntryRequiresGraphRepartition");
                                var id = int.Parse(await s.ResolveAsync("Duty", row.Id, ct));
                                var owned = await db
                                    .AssignmentEntries.Include(x => x.Event)
                                    .SingleAsync(x => x.Id == id, ct);
                                var priorLinks = await db
                                    .ShiftAssignmentEntries.Include(x => x.Users)
                                    .Where(x => x.AssignmentEntryId == id)
                                    .ToListAsync(ct);
                                foreach (var old in priorLinks)
                                    db.ShiftAssignmentEntries.Remove(old);
                                await db.SaveChangesAsync(ct);
                                owned.AssignmentDefinitionId = definitionId;
                                owned.Capacity =
                                    live.Count == 1 && (live[0].Bool("IsClosed") || live[0].Bool("IsNotRequired"))
                                        ? 0
                                        : 1;
                                owned.CategoryId = definition.CategoryId;
                                owned.SubCategoryId = definition.SubCategoryId;
                                owned.UpdatedOn = s.Now;
                                var e = owned.Event!;
                                e.Title = definition.Name;
                                e.Description = row.Text("Comment");
                                e.StartAtUtc = dates.Start;
                                e.EndAtUtc = dates.End;
                                e.TimeZoneId = zone;
                                e.LocationId = location;
                                e.Color = definition.Color;
                                e.AllDay = false;
                                e.StatusTypeCode = SsLeaveWriter.EventStatus(dates.Start, zone, row.Date("ExpiryDate"));
                                e.CancelledAt = row.Date("ExpiryDate");
                                e.UpdatedOn = s.Now;
                                await db.SaveChangesAsync(ct);
                                foreach (var slot in live)
                                {
                                    var result = ("MigratedWithDifference", (string?)"SlotFlagsRepresentedOnEntry");
                                    if (
                                        slot.Guid("SheriffId") is { } sheriff
                                        && !slot.Bool("IsClosed")
                                        && !slot.Bool("IsNotRequired")
                                        && !slot.Bool("IsNotAvailable")
                                    )
                                    {
                                        var user = Guid.Parse(await s.ResolveAsync("User", sheriff.ToString(), ct));
                                        var slotDates = SsWriteSession.Dates(slot);
                                        var localDay = DateOnly.FromDateTime(
                                            TimeZoneInfo
                                                .ConvertTime(dates.Start, TimeZoneInfo.FindSystemTimeZoneById(zone))
                                                .DateTime
                                        );
                                        if (row.Date("ExpiryDate") is null && localDay > new DateOnly(2026, 12, 1))
                                        {
                                            var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
                                            var from = new DateTimeOffset(
                                                TimeZoneInfo.ConvertTimeToUtc(
                                                    localDay.ToDateTime(TimeOnly.MinValue),
                                                    tz
                                                )
                                            );
                                            var to = new DateTimeOffset(
                                                TimeZoneInfo.ConvertTimeToUtc(
                                                    localDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
                                                    tz
                                                )
                                            );
                                            if (
                                                await db.ShiftAssignmentEntryUsers.AnyAsync(
                                                    x =>
                                                        x.UserId == user
                                                        && x.ShiftAssignmentEntry!.AssignmentEntryId != id
                                                        && x.ShiftAssignmentEntry
                                                            .AssignmentEntry!
                                                            .AssignmentDefinitionId == definitionId
                                                        && x.ShiftAssignmentEntry.AssignmentEntry.Event!.StatusTypeCode
                                                            != "cancelled"
                                                        && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc
                                                            >= from
                                                        && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc < to,
                                                    ct
                                                )
                                            )
                                                throw new SsReject("FutureAssignmentUniquenessConflict");
                                        }
                                        var shifts = await db
                                            .ShiftEntries.Include(x => x.Event)
                                            .Where(x =>
                                                x.Event != null
                                                && x.Event.LocationId == location
                                                && x.Event.StatusTypeCode != "cancelled"
                                                && x.Event.StartAtUtc < slotDates.End
                                                && x.Event.EndAtUtc > slotDates.Start
                                                && x.Users.Any(y => y.UserId == user)
                                            )
                                            .ToListAsync(ct);
                                        foreach (var shift in shifts)
                                        {
                                            var link = s.Stamp(
                                                new ShiftAssignmentEntry
                                                {
                                                    AssignmentEntryId = id,
                                                    ShiftEntryId = shift.Id,
                                                }
                                            );
                                            link.Users.Add(s.Stamp(new ShiftAssignmentEntryUser { UserId = user }));
                                            db.ShiftAssignmentEntries.Add(link);
                                        }
                                        result =
                                            shifts.Count == 0
                                                ? ("DroppedBadData", "NoOverlappingShiftUserLinkDropped")
                                                : ("Migrated", null);
                                        if (
                                            shifts.Count > 0
                                            && !shifts.Any(x =>
                                                x.Event!.StartAtUtc <= slotDates.Start
                                                && x.Event.EndAtUtc >= slotDates.End
                                            )
                                        )
                                            result = ("MigratedWithDifference", "SlotPartlyOutsideShift");
                                    }
                                    slotOutcomes[slot.Id] = result;
                                    await db.SaveChangesAsync(ct);
                                    var crosswalk = await db.DataMigrationRecords.SingleOrDefaultAsync(
                                        x => x.Source == "SS" && x.EntityType == "DutySlot" && x.LegacyId == slot.Id,
                                        ct
                                    );
                                    if (crosswalk is null)
                                        await s.LinkAsync(
                                            "DutySlot",
                                            slot,
                                            "AssignmentEntry",
                                            id,
                                            SsWriteSession.Hash(new { DutyId = row.Id, SlotId = slot.Id }),
                                            false,
                                            ct
                                        );
                                    else
                                    {
                                        crosswalk.SourceUpdatedAtUtc = slot.Date("UpdatedOn") ?? slot.Date("CreatedOn");
                                        crosswalk.LastAppliedRunId = s.Run.Id;
                                    }
                                }
                            }
                        )
                    )
                        return;
                    var bySheriff = live.Where(x =>
                            x.Guid("SheriffId") is not null
                            && !x.Bool("IsClosed")
                            && !x.Bool("IsNotRequired")
                            && !x.Bool("IsNotAvailable")
                        )
                        .GroupBy(x => x.Guid("SheriffId"))
                        .Select(x => x.ToList())
                        .ToList();
                    var groups = live.Count <= 1 || bySheriff.Count == 0 ? new List<List<SsRow>> { live } : bySheriff;
                    int? primaryEntryId = null;
                    var entries = new List<(AssignmentEntry Entry, string Key)>();
                    foreach (var slots in groups)
                    {
                        var start =
                            live.Count > 1 && bySheriff.Count > 0
                                ? slots.Min(x => x.Date("StartDate"))!.Value
                                : dates.Start;
                        var end =
                            live.Count > 1 && bySheriff.Count > 0
                                ? slots.Max(x => x.Date("EndDate"))!.Value
                                : dates.End;
                        var targetEvent = s.Stamp(
                            new Event
                            {
                                Title = definition.Name,
                                Description = row.Text("Comment"),
                                StartAtUtc = start,
                                EndAtUtc = end,
                                AllDay = false,
                                TimeZoneId = zone,
                                LocationId = location,
                                Color = definition.Color,
                                SourceModule = "scheduling",
                                EventTypeCode = "assignment",
                                StatusTypeCode = SsLeaveWriter.EventStatus(start, zone, row.Date("ExpiryDate")),
                                CancelledAt = row.Date("ExpiryDate"),
                            }
                        );
                        var entry = s.Stamp(
                            new AssignmentEntry
                            {
                                Event = targetEvent,
                                AssignmentDefinitionId = definitionId,
                                Capacity =
                                    slots.Count == 1 && (slots[0].Bool("IsClosed") || slots[0].Bool("IsNotRequired"))
                                        ? 0
                                        : 1,
                                CategoryId = definition.CategoryId,
                                SubCategoryId = definition.SubCategoryId,
                            }
                        );
                        db.AssignmentEntries.Add(entry);
                        await db.SaveChangesAsync(ct);
                        primaryEntryId ??= entry.Id;
                        entries.Add(
                            (
                                entry,
                                $"duty:{row.Id}:segment:{(live.Count > 1 && bySheriff.Count > 0 ? slots[0].Guid("SheriffId")?.ToString() : "whole")}"
                            )
                        );
                        foreach (var slot in slots)
                        {
                            if (
                                slot.Guid("SheriffId") is not { } sheriff
                                || slot.Bool("IsClosed")
                                || slot.Bool("IsNotRequired")
                                || slot.Bool("IsNotAvailable")
                            )
                            {
                                slotOutcomes[slot.Id] = ("MigratedWithDifference", "SlotFlagsRepresentedOnEntry");
                                continue;
                            }
                            var user = Guid.Parse(await s.ResolveAsync("User", sheriff.ToString(), ct));
                            var slotStart = slot.Date("StartDate")!.Value;
                            var slotEnd = slot.Date("EndDate")!.Value;
                            var shifts = await db
                                .ShiftEntries.Where(x =>
                                    x.Event != null
                                    && x.Event.LocationId == location
                                    && x.Event.StatusTypeCode != "cancelled"
                                    && x.Event.StartAtUtc < slotEnd
                                    && x.Event.EndAtUtc > slotStart
                                    && x.Users.Any(y => y.UserId == user)
                                )
                                .ToListAsync(ct);
                            if (shifts.Count == 0)
                            {
                                slotOutcomes[slot.Id] = ("DroppedBadData", "NoOverlappingShiftUserLinkDropped");
                                continue;
                            }
                            foreach (var shift in shifts)
                            {
                                var localDay = DateOnly.FromDateTime(
                                    TimeZoneInfo.ConvertTime(start, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime
                                );
                                if (row.Date("ExpiryDate") is null && localDay > new DateOnly(2026, 12, 1))
                                {
                                    var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
                                    var from = new DateTimeOffset(
                                        TimeZoneInfo.ConvertTimeToUtc(localDay.ToDateTime(TimeOnly.MinValue), tz)
                                    );
                                    var to = new DateTimeOffset(
                                        TimeZoneInfo.ConvertTimeToUtc(
                                            localDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
                                            tz
                                        )
                                    );
                                    if (
                                        await db.ShiftAssignmentEntryUsers.AnyAsync(
                                            x =>
                                                x.UserId == user
                                                && x.ShiftAssignmentEntry!.AssignmentEntryId != entry.Id
                                                && x.ShiftAssignmentEntry.AssignmentEntry!.AssignmentDefinitionId
                                                    == definitionId
                                                && x.ShiftAssignmentEntry.AssignmentEntry.Event!.StatusTypeCode
                                                    != "cancelled"
                                                && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc >= from
                                                && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc < to,
                                            ct
                                        )
                                    )
                                        throw new SsReject("FutureAssignmentUniquenessConflict");
                                }
                                var link = await db
                                    .ShiftAssignmentEntries.Include(x => x.Users)
                                    .SingleOrDefaultAsync(
                                        x => x.ShiftEntryId == shift.Id && x.AssignmentEntryId == entry.Id,
                                        ct
                                    );
                                if (link is null)
                                {
                                    link = s.Stamp(
                                        new ShiftAssignmentEntry
                                        {
                                            ShiftEntryId = shift.Id,
                                            AssignmentEntryId = entry.Id,
                                        }
                                    );
                                    db.ShiftAssignmentEntries.Add(link);
                                }
                                if (!link.Users.Any(x => x.UserId == user))
                                    link.Users.Add(s.Stamp(new ShiftAssignmentEntryUser { UserId = user }));
                                await db.SaveChangesAsync(ct);
                            }
                            var containing = await db.ShiftEntries.AnyAsync(
                                x =>
                                    shifts.Select(y => y.Id).Contains(x.Id)
                                    && x.Event!.StartAtUtc <= slotStart
                                    && x.Event.EndAtUtc >= slotEnd,
                                ct
                            );
                            slotOutcomes[slot.Id] = containing
                                ? ("Migrated", null)
                                : ("MigratedWithDifference", "SlotPartlyOutsideShift");
                            await s.LinkAsync(
                                "DutySlot",
                                slot,
                                "AssignmentEntry",
                                entry.Id,
                                SsWriteSession.Hash(
                                    new
                                    {
                                        SlotId = slot.Id,
                                        EntryId = entry.Id,
                                        user,
                                    }
                                ),
                                false,
                                ct,
                                slotOutcomes[slot.Id].Outcome,
                                slotOutcomes[slot.Id].Reason
                            );
                            // Final source coverage is emitted once, after all duty savepoints complete.
                        }
                    }
                    foreach (var slot in live.Where(x => !groups.Any(g => g.Contains(x))))
                        slotOutcomes[slot.Id] = ("MigratedWithDifference", "FlagOnlySegmentBesideStaffDropped");
                    foreach (var slot in live)
                    {
                        if (
                            !s.Rebinding.Contains(("DutySlot", slot.Id))
                            && await db.DataMigrationRecords.AnyAsync(
                                x => x.Source == "SS" && x.EntityType == "DutySlot" && x.LegacyId == slot.Id,
                                ct
                            )
                        )
                            continue;
                        await s.LinkAsync(
                            "DutySlot",
                            slot,
                            "AssignmentEntry",
                            primaryEntryId!.Value,
                            SsWriteSession.Hash(new { DutyId = row.Id, SlotId = slot.Id }),
                            false,
                            ct
                        );
                    }
                    foreach (var (entry, key) in entries)
                        await s.LinkAsync(
                            "DutyEntry",
                            new SsRow(
                                new Dictionary<string, object?>
                                {
                                    ["Id"] = key,
                                    ["CreatedOn"] = null,
                                    ["UpdatedOn"] = null,
                                }
                            ),
                            "AssignmentEntry",
                            entry.Id,
                            hash,
                            true,
                            ct
                        );
                    await s.LinkAsync(
                        "Duty",
                        row,
                        "AssignmentEntry",
                        primaryEntryId!.Value,
                        hash,
                        true,
                        ct,
                        live.Count > 1 ? "MigratedWithDifference" : "Migrated",
                        live.Count > 1 ? "DutySplitByDistinctSheriff" : null
                    );
                    await s.SealNewDutyEntriesAsync(entries.Select(x => x.Entry.Id).ToArray(), ct);
                },
                ct
            );
            if (proposals is not null)
                continue;
            var dutyOutcome =
                s.FindOutcome("Duty", row.Id)
                ?? throw new InvalidOperationException("Sequence contains no matching element");
            if (dutyOutcome.Outcome is "Rejected" or "Failed" or "Excluded")
                foreach (var slot in relevantSlots)
                    slotOutcomes[slot.Id] = (dutyOutcome.Outcome, "ParentDutyNotMigrated");
        }
        if (proposals is not null)
            return;
        var filled = rows["DutySlot"]
            .Where(x =>
                x.Date("EndDate") >= boundary && x.Date("ExpiryDate") is null && x.Guid("SheriffId") is not null
            )
            .ToList();
        var missing = filled
            .Where(x => slotOutcomes.TryGetValue(x.Id, out var o) && o.Outcome == "DroppedBadData")
            .ToList();
        var material = filled.Count > 0 && (double)missing.Count / filled.Count > 0.001;
        foreach (var slot in missing)
        {
            var zone = SsWriteSession.Timezone(slot, true)!;
            var future =
                DateOnly.FromDateTime(
                    TimeZoneInfo
                        .ConvertTime(slot.Date("StartDate")!.Value, TimeZoneInfo.FindSystemTimeZoneById(zone))
                        .DateTime
                ) > new DateOnly(2026, 12, 1);
            if (material || future)
                slotOutcomes[slot.Id] = ("Failed", "MaterialMissingShiftUserLink");
        }
        foreach (var slot in rows["DutySlot"])
        {
            if (slot.Date("EndDate") < boundary)
                s.Outcome("DutySlot", slot.Id, "Excluded", "BeforeHistoryBoundary");
            else if (slot.Date("ExpiryDate") is not null)
                s.Outcome("DutySlot", slot.Id, "Excluded", "DeletedSlot");
            else if (slotOutcomes.TryGetValue(slot.Id, out var result))
                s.Outcome("DutySlot", slot.Id, result.Outcome, result.Reason);
            else if (!selectedDuties.ContainsKey(slot.Int("DutyId")?.ToString() ?? ""))
                s.Outcome("DutySlot", slot.Id, "Rejected", "MissingDuty");
            else
            {
                var record = await db.DataMigrationRecords.SingleOrDefaultAsync(
                    x => x.Source == "SS" && x.EntityType == "DutySlot" && x.LegacyId == slot.Id,
                    ct
                );
                var prior = record is null
                    ? null
                    : await db.DataMigrationOutcomes.SingleOrDefaultAsync(
                        x => x.RunId == record.LastAppliedRunId && x.EntityType == "DutySlot" && x.LegacyId == slot.Id,
                        ct
                    );
                s.Outcome(
                    "DutySlot",
                    slot.Id,
                    prior?.Outcome ?? "NeedsAction",
                    prior?.Reason ?? (prior is null ? "ExistingDutySlotReconciliationRequired" : "ExistingCrosswalk")
                );
            }
        }
    }

    internal static void CompleteGroupOutcomes(SsWriteSession s, string type, IEnumerable<SsRow> members)
    {
        var list = members.ToList();
        // Outcome keys are unique, so the last matching outcome is the member outcome with the
        // highest list position.
        var at = list.Select(x => s.OutcomePosition(type, x.Id)).DefaultIfEmpty(-1).Max();
        var failure = at < 0 ? null : s.Outcomes[at];
        foreach (var row in list.Where(x => s.FindOutcome(type, x.Id) is null))
            s.Outcome(type, row.Id, failure?.Outcome ?? "Failed", failure?.Reason ?? "GroupWriteFailed");
    }
}
