using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.Training;

namespace Unified.DataMigration.Services;

public sealed class SsTrainingWriter(UnifiedDbContext db)
{
    // Location is a read-only lookup (home location time zone, see SsTrainingRules.VancouverSheriffs).
    public static readonly string[] Tables =
    [
        "LookupCode",
        "Location",
        "User",
        "SheriffTraining",
        "SheriffStandardTraining",
    ];

    internal static string AllocateCourseCode(string code, IReadOnlySet<string> reserved) =>
        SsTrainingRules.AllocateCourseCode(code, reserved);

    // Training has no history date limit: every SheriffTraining row is considered regardless of
    // HistoryStartDate (which still bounds MyTeam/Leave/Scheduling and lineage/checkpoint keys).
    // includeExcused = DataMigration:Sources:SS:Jobs:Training:IncludeExcused.
    public async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        bool includeExcused,
        CancellationToken ct
    )
    {
        var users = rows["User"].ToDictionary(x => x.Id);
        var sourceCodes = SsTrainingRules.SourceCourseCodes(rows["LookupCode"]);
        var shortenedCarbineCourseIds = SsTrainingRules.ShortenedCarbineCourseIds(rows["LookupCode"]);
        var courseCodeCounts = SsTrainingRules.CourseCodeCounts(rows["LookupCode"]);
        var repeats = SsTrainingRules.RepeatedCourses(rows["SheriffTraining"], users, includeExcused);
        var vancouver = SsTrainingRules.VancouverSheriffs(rows["User"], rows["Location"]);
        foreach (var row in SsTrainingRules.Courses(rows["LookupCode"]))
            await session.RowAsync(
                "Training",
                row,
                async () =>
                {
                    var mapping = SsTrainingRules.MapCourse(row, courseCodeCounts);
                    var code = mapping.Code;
                    var hash = mapping.Hash;
                    if (await session.ReplayAsync("Training", row, hash, true, ct))
                        return;
                    await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Trainings\" IN SHARE ROW EXCLUSIVE MODE", ct);
                    var shortened = code.Length > 50;
                    if (shortened)
                    {
                        var reserved = (await db.Trainings.Select(x => x.Code).ToListAsync(ct)).ToHashSet(
                            StringComparer.Ordinal
                        );
                        reserved.UnionWith(sourceCodes);
                        code = AllocateCourseCode(code, reserved);
                    }
                    var targets = await db.Trainings.Where(x => x.Code == code).ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousCourse");
                    var target = targets.SingleOrDefault();
                    var created = target is null;
                    if (target is null)
                    {
                        var category = SsTrainingRules.ApprovedCategory(row);
                        int? categoryId = null;
                        if (category is not null)
                        {
                            var categories = await db.TrainingCategories.Where(x => x.Name == category).ToListAsync(ct);
                            if (categories.Count > 1)
                                throw new SsReject("AmbiguousTrainingCategory");
                            var existing = categories.SingleOrDefault();
                            if (existing is null)
                            {
                                existing = session.Stamp(new TrainingCategory { Name = category, Description = null });
                                db.TrainingCategories.Add(existing);
                                await db.SaveChangesAsync(ct);
                            }
                            categoryId = existing.Id;
                        }
                        var max = await db.Trainings.Select(x => (int?)x.Order).MaxAsync(ct);
                        if (max == int.MaxValue)
                            throw new SsReject("OrderOverflow");
                        target = session.Stamp(
                            new Training
                            {
                                Code = code,
                                Description = mapping.Description,
                                EffectiveDate = row.Date("EffectiveDate") ?? session.Now,
                                ExpiryDate = row.Date("ExpiryDate"),
                                Mandatory = row.Bool("Mandatory"),
                                Rotating = SsTrainingRules.Rotating(row, mapping, repeats),
                                ValidityDays = mapping.Days,
                                AdvanceNoticeDays = SsTrainingRules.AdvanceNoticeDays(mapping),
                                TrainingCategoryId = categoryId,
                                Order = max is null ? 0 : max.Value + 1,
                            }
                        );
                        db.Trainings.Add(target);
                        await db.SaveChangesAsync(ct);
                        if (SsTrainingRules.CarbineCourses.Contains(code))
                        {
                            if (!target.Mandatory)
                                throw new SsReject("CarbineCourseNotMandatory");
                            var profile = await CarbineProfileAsync(session.Now, ct);
                            db.TrainingProfileRequirements.Add(
                                session.Stamp(
                                    new TrainingProfileRequirement
                                    {
                                        TrainingId = target.Id,
                                        TrainingProfileTypeId = profile.Id,
                                    }
                                )
                            );
                        }
                    }
                    var (linkOutcome, linkReason) = SsTrainingRules.CourseLinkOutcome(shortened);
                    await session.LinkAsync(
                        "Training",
                        row,
                        "Training",
                        target.Id,
                        hash,
                        created,
                        ct,
                        linkOutcome,
                        linkReason
                    );
                },
                ct
            );
        foreach (var row in SsTrainingRules.HistoryOrder(rows["SheriffTraining"], vancouver))
            await session.RowAsync(
                "SheriffTraining",
                row,
                async () =>
                {
                    var sourceUser = row.Guid("SheriffId")?.ToString() ?? throw new SsReject("MissingUser");
                    if (!users.TryGetValue(sourceUser, out var sheriff))
                        throw new SsReject("MissingSourceUser");
                    var excused = SsTrainingRules.ExcludedExcused(sheriff, includeExcused);
                    if (row.Date("ExpiryDate") is not null || excused)
                    {
                        var linked = await db.DataMigrationRecords.AnyAsync(
                            x => x.Source == "SS" && x.EntityType == "SheriffTraining" && x.LegacyId == row.Id,
                            ct
                        );
                        session.Outcome(
                            "SheriffTraining",
                            row.Id,
                            "Excluded",
                            SsTrainingRules.ExcludedHistoryReason(linked, excused)
                        );
                        return;
                    }
                    // America/Vancouver home location: old-rule standard-time instants from 2026-11-01 move -1h.
                    // Applies to incoming SS values only (inserts, changed SS times); Unified times are kept.
                    var dates = SsTrainingRules.MapHistoryTimes(
                        row,
                        SsTrainingRules.IsVancouverSheriff(row, vancouver)
                    );
                    SsWriteSession.Timezone(row, false); // Target UserTraining has no timezone column; report the loss.
                    var hash = SsTrainingRules.HistoryHash(row, sourceUser);
                    // Unchanged source: no write (ExistingCrosswalk/UnchangedMappedSource); the time adjustment is
                    // not part of the hash, so it never rewrites a migrated history. Changed source with a target
                    // edited in Unified since migration: skipped, not overwritten, unless the edit already equals
                    // what the job would write (adopted, no write). An owned update keeps the target's times
                    // (KeepTargetTimes); changed SS dates that change its order within the user/course group are
                    // placed again (PlaceHistory) so the newest history stays the highest version.
                    Reorder? reorder = null;
                    if (
                        await session.ReplaySkippingDriftAsync(
                            "SheriffTraining",
                            row,
                            hash,
                            ct,
                            async () =>
                            {
                                var id = int.Parse(await session.ResolveAsync("SheriffTraining", row.Id, ct));
                                var owned = await db
                                    .UserTrainings.Include(x => x.Training)
                                    .SingleAsync(x => x.Id == id, ct);
                                var kept = SsTrainingRules.KeepTargetTimes(
                                    row,
                                    dates,
                                    owned.AwardedOn,
                                    owned.EndingOn,
                                    owned.ExpiryDate
                                );
                                var resolvedUser = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
                                var resolvedCourse =
                                    await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct)
                                    ?? throw new SsReject("MissingTraining");
                                if (owned.UserId != resolvedUser || owned.TrainingId != resolvedCourse)
                                {
                                    await db.Database.ExecuteSqlRawAsync(
                                        "LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE",
                                        ct
                                    );
                                    owned.Version = SsTrainingRules.NextVersion(
                                        await db
                                            .UserTrainings.Where(x =>
                                                x.UserId == resolvedUser
                                                && x.TrainingId == resolvedCourse
                                                && x.Id != owned.Id
                                            )
                                            .Select(x => (int?)x.Version)
                                            .MaxAsync(ct)
                                    );
                                    owned.UserId = resolvedUser;
                                    owned.TrainingId = resolvedCourse;
                                    owned.Training = await db.Trainings.SingleAsync(x => x.Id == resolvedCourse, ct);
                                }
                                else if (reorder is { } move)
                                    await ReorderAsync(session, owned, move, ct);
                                owned.AwardedOn = kept.Start;
                                owned.EndingOn = kept.End;
                                owned.ExpiryDate = kept.Expiry;
                                owned.Notes = SsTrainingRules.MappedNotes(row, owned.Training.Code);
                                owned.NoticeState = SsTrainingRules.NoticeState(row);
                                StampRun(owned, session.Now);
                                // Never invoke renewal or mail services.
                            },
                            () => TargetMatchesMappedAsync(session, row, sourceUser, dates, ct),
                            async () =>
                            {
                                (reorder, var skip) = await PlanReorderAsync(session, row, sourceUser, dates, ct);
                                return skip;
                            }
                        )
                    )
                        return;
                    var userId = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
                    var courseId =
                        await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct)
                        ?? throw new SsReject("MissingTraining");
                    var course = await db.Trainings.SingleAsync(x => x.Id == courseId, ct);
                    var notes = SsTrainingRules.MappedNotes(row, course.Code);
                    await db.Database.ExecuteSqlRawAsync(
                        "LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE",
                        ct
                    );
                    var histories = await db
                        .UserTrainings.Where(x => x.UserId == userId && x.TrainingId == courseId)
                        .ToListAsync(ct);
                    // Keep the first-run (EndDate, StartDate, ID) order so a backfilled older history never
                    // displaces the current certificate (the highest version).
                    var (placement, owners) = await PlaceAsync(session, histories, row.Id, dates, ct);
                    if (placement.Skip is { } skip)
                    {
                        session.Outcome("SheriffTraining", row.Id, "Excluded", skip);
                        return;
                    }
                    var byId = histories.ToDictionary(x => x.Id);
                    foreach (var shifted in placement.Shift)
                    {
                        var existing = byId[shifted];
                        await session.RenumberOwnedAsync(
                            owners[shifted],
                            () =>
                            {
                                existing.Version = SsTrainingRules.NextVersion(existing.Version);
                                StampRun(existing, session.Now);
                            },
                            ct
                        );
                    }
                    var version = placement.Version;
                    var target = session.Stamp(
                        new UserTraining
                        {
                            UserId = userId,
                            TrainingId = courseId,
                            Version = version,
                            AwardedOn = dates.Start,
                            EndingOn = dates.End,
                            AllDay = false,
                            ExpiryDate = dates.Expiry,
                            Notes = notes,
                            NoticeState = SsTrainingRules.NoticeState(row),
                        }
                    );
                    db.UserTrainings.Add(target);
                    // Only Training owns this user field; do not emit user-created/renewal/email signals.
                    // Keep the source keyword when shortening or later cleanup removes it from the target label.
                    if (
                        SsTrainingRules.IsCarbineHistory(
                            course.Code,
                            row.Int("TrainingTypeId"),
                            shortenedCarbineCourseIds
                        )
                    )
                    {
                        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
                        if (user.TrainingProfileId is null)
                        {
                            var profile = (await CarbineProfileAsync(session.Now, ct)).Id;
                            await session.ComposeAsync(
                                "User",
                                [sourceUser],
                                () =>
                                {
                                    user.TrainingProfileId = profile;
                                    user.UpdatedOn = session.Now;
                                    return Task.CompletedTask;
                                },
                                ct
                            );
                        }
                    }
                    await db.SaveChangesAsync(ct);
                    var older = histories.Any(x => x.EndingOn > dates.End);
                    await session.LinkAsync(
                        "SheriffTraining",
                        row,
                        "UserTraining",
                        target.Id,
                        hash,
                        true,
                        ct,
                        "MigratedWithDifference",
                        SsTrainingRules.HistoryLinkReason(row, older)
                    );
                },
                ct
            );
        foreach (var row in rows["SheriffStandardTraining"])
            session.Outcome("SheriffStandardTraining", row.Id, "Excluded", "UnifiedMandatoryPolicyReplacesFutureRules");
    }

    /// <summary>
    /// Sets the run's UpdatedOn. A history already saved in this run (shifted by a backfill, then shifted again or
    /// updated) keeps the run stamp: AuditableEntityInterceptor would replace an unchanged UpdatedOn with the wall
    /// clock, which Bulk (one statement per row) never does.
    /// </summary>
    private void StampRun(UserTraining history, DateTimeOffset now)
    {
        if (history.UpdatedOn == now)
        {
            var updatedOn = db.Entry(history).Property(x => x.UpdatedOn);
            updatedOn.OriginalValue = null;
            updatedOn.IsModified = true;
        }
        history.UpdatedOn = now;
    }

    /// <summary>
    /// True when the drifted owned target already holds exactly what this job would write (its times kept per
    /// <see cref="SsTrainingRules.KeepTargetTimes"/>). Any mismatch, or a mapping that would reject, keeps the
    /// OwnedTargetDrift skip.
    /// </summary>
    private async Task<bool> TargetMatchesMappedAsync(
        SsWriteSession session,
        SsRow row,
        string sourceUser,
        SsTrainingRules.HistoryTimes dates,
        CancellationToken ct
    )
    {
        try
        {
            var id = int.Parse(await session.ResolveAsync("SheriffTraining", row.Id, ct), CultureInfo.InvariantCulture);
            var owned = await db.UserTrainings.Include(x => x.Training).SingleAsync(x => x.Id == id, ct);
            var user = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
            var course = await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct);
            if (course is null || owned.UserId != user || owned.TrainingId != course)
                return false;
            var actual = new SsTrainingRules.HistoryTargetValues(
                owned.UserId,
                owned.TrainingId,
                owned.AwardedOn,
                owned.EndingOn,
                owned.AllDay,
                owned.ExpiryDate,
                owned.Notes,
                owned.NoticeState
            );
            return actual
                == SsTrainingRules.MappedTarget(
                    row,
                    user,
                    course.Value,
                    owned.Training.Code,
                    SsTrainingRules.KeepTargetTimes(row, dates, owned.AwardedOn, owned.EndingOn, owned.ExpiryDate)
                );
        }
        catch (SsReject)
        {
            return false;
        }
    }

    private async Task<(
        SsTrainingRules.HistoryPlacement Placement,
        Dictionary<int, DataMigrationRecord> Owners
    )> PlaceAsync(
        SsWriteSession session,
        List<UserTraining> histories,
        string legacyId,
        SsTrainingRules.HistoryTimes dates,
        CancellationToken ct
    )
    {
        if (!SsTrainingRules.MayHaveNewerHistory(histories.Select(x => x.EndingOn), dates.End))
            return (
                new(SsTrainingRules.NextVersion(histories.Select(x => (int?)x.Version).Max()), [], null),
                new Dictionary<int, DataMigrationRecord>()
            );
        var (slots, blocks, owners) = await SlotsAsync(session, histories, ct);
        return (SsTrainingRules.PlaceHistory(slots, legacyId, dates.Start, dates.End, x => blocks[x.Id]), owners);
    }

    /// <summary>A planned re-placement of an updated owned history (see <see cref="PlanReorderAsync"/>).</summary>
    private sealed record Reorder(
        SsTrainingRules.HistoryPlacement Placement,
        Dictionary<int, DataMigrationRecord> Owners,
        Dictionary<int, UserTraining> Histories
    );

    /// <summary>
    /// For an undrifted owned history whose SS dates changed (times kept per
    /// <see cref="SsTrainingRules.KeepTargetTimes"/>; the Vancouver adjustment alone never moves a migrated
    /// history) without a user/course rebind: when the new dates change its order among the other histories of
    /// the group, plans its new version with <see cref="SsTrainingRules.PlaceHistory"/> (its current version is a
    /// free gap, so only the newer owned run between moves). Returns the plan, or the blocking reason (an
    /// unowned/drifted row would have to move) so the caller skips the whole update instead of changing the
    /// dates while leaving a stale version order. Null plan and reason: keep the existing version.
    /// </summary>
    private async Task<(Reorder? Plan, string? Skip)> PlanReorderAsync(
        SsWriteSession session,
        SsRow row,
        string sourceUser,
        SsTrainingRules.HistoryTimes mapped,
        CancellationToken ct
    )
    {
        var id = int.Parse(await session.ResolveAsync("SheriffTraining", row.Id, ct), CultureInfo.InvariantCulture);
        var owned = await db.UserTrainings.SingleAsync(x => x.Id == id, ct);
        var user = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
        var course = await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct);
        var dates = SsTrainingRules.KeepTargetTimes(row, mapped, owned.AwardedOn, owned.EndingOn, owned.ExpiryDate);
        if (
            course is null
            || owned.UserId != user
            || owned.TrainingId != course
            || owned.AwardedOn == dates.Start && owned.EndingOn == dates.End
        )
            return (null, null);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE", ct);
        var others = await db
            .UserTrainings.Where(x => x.UserId == user && x.TrainingId == course && x.Id != id)
            .ToListAsync(ct);
        var (slots, blocks, owners) = await SlotsAsync(session, others, ct);
        if (
            !SsTrainingRules.HistoryOrderChanged(slots, row.Id, owned.AwardedOn, owned.EndingOn, dates.Start, dates.End)
        )
            return (null, null);
        var placement = SsTrainingRules.PlaceHistory(slots, row.Id, dates.Start, dates.End, x => blocks[x.Id]);
        return placement.Skip is { } skip
            ? (null, skip)
            : (new(placement, owners, others.ToDictionary(x => x.Id)), null);
    }

    /// <summary>
    /// Applies a <see cref="Reorder"/> plan: parks the updated history on -Id (Version is unique per
    /// user/course), moves the newer owned run up one version with refreshed crosswalk evidence, then gives
    /// the updated history its placed version (saved with the owned update).
    /// </summary>
    private async Task ReorderAsync(SsWriteSession session, UserTraining owned, Reorder plan, CancellationToken ct)
    {
        owned.Version = -owned.Id;
        StampRun(owned, session.Now);
        await db.SaveChangesAsync(ct);
        foreach (var shifted in plan.Placement.Shift)
        {
            var existing = plan.Histories[shifted];
            await session.RenumberOwnedAsync(
                plan.Owners[shifted],
                () =>
                {
                    existing.Version = SsTrainingRules.NextVersion(existing.Version);
                    StampRun(existing, session.Now);
                },
                ct
            );
        }
        owned.Version = plan.Placement.Version;
    }

    private async Task<(
        List<SsTrainingRules.HistorySlot> Slots,
        Dictionary<int, string?> Blocks,
        Dictionary<int, DataMigrationRecord> Owners
    )> SlotsAsync(SsWriteSession session, List<UserTraining> histories, CancellationToken ct)
    {
        var owners = new Dictionary<int, DataMigrationRecord>();
        var keys = histories.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        var records = (
            await db
                .DataMigrationRecords.Where(x =>
                    x.Source == "SS"
                    && x.EntityType == "SheriffTraining"
                    && x.TargetType == "UserTraining"
                    && keys.Contains(x.TargetKey)
                )
                .OrderBy(x => x.Id)
                .ToListAsync(ct)
        ).ToLookup(x => x.TargetKey, StringComparer.Ordinal);
        var slots = new List<SsTrainingRules.HistorySlot>();
        var blocks = new Dictionary<int, string?>();
        foreach (var history in histories)
        {
            var linked = records[history.Id.ToString(CultureInfo.InvariantCulture)].ToList();
            var owner = linked.FirstOrDefault(x => x.CreatedByRunId is not null);
            if (owner is not null)
                owners[history.Id] = owner;
            blocks[history.Id] = owner is null
                ? SsTrainingRules.UnownedNewerHistoryReason
                : await session.OwnedTargetBlockAsync(owner, ct);
            slots.Add(
                new(
                    history.Id,
                    history.Version,
                    history.AwardedOn,
                    history.EndingOn,
                    (owner ?? linked.FirstOrDefault())?.LegacyId
                )
            );
        }
        return (slots, blocks, owners);
    }

    private async Task<TrainingProfileType> CarbineProfileAsync(DateTimeOffset now, CancellationToken ct)
    {
        var profiles = await db
            .TrainingProfileTypes.Where(x =>
                x.Code == "CARBINE_OPERATOR" && x.EffectiveDate <= now && (x.ExpiryDate == null || x.ExpiryDate > now)
            )
            .ToListAsync(ct);
        if (profiles.Count != 1)
            throw new SsReject("MissingActiveCarbineProfile");
        return profiles[0];
    }
}
