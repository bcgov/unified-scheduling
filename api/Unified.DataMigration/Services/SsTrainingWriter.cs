using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.DataMigration.Services;

public sealed class SsTrainingWriter(UnifiedDbContext db)
{
    public static readonly string[] Tables = ["LookupCode", "User", "SheriffTraining", "SheriffStandardTraining"];
    private static readonly HashSet<string> CarbineCourses =
    [
        "Carbine Initial Course",
        "Carbine Practice 1",
        "Carbine Practice 2",
        "Carbine Annual Recertification",
    ];

    private static string? MappedNotes(SsRow row, Training course)
    {
        var comment = row.Text("Comment");
        var note = row.Text("Note");
        if (course.Code != "Other")
        {
            if (!string.IsNullOrEmpty(comment))
                throw new SsReject("UnexpectedNonOtherComment");
            return note;
        }
        return string.IsNullOrEmpty(comment)
            ? note
            : $"Other training: {comment}" + (string.IsNullOrEmpty(note) || note == comment ? "" : "\n" + note);
    }

    public async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        var users = rows["User"].ToDictionary(x => x.Id);
        bool Eligible(SsRow history) =>
            history.Date("EndDate") >= boundary
            && history.Date("ExpiryDate") is null
            && users.TryGetValue(history.Guid("SheriffId")?.ToString() ?? "", out var user)
            && !user.Bool("Excused");
        var repeats = rows["SheriffTraining"]
            .Where(Eligible)
            .GroupBy(x => (User: x.Guid("SheriffId"), Course: x.Int("TrainingTypeId")))
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.Course)
            .ToHashSet();
        foreach (var row in rows["LookupCode"].Where(x => x.Int("Type") == 6).OrderBy(x => x.Int("Id")))
            await session.RowAsync(
                "Training",
                row,
                async () =>
                {
                    var code = row.Text("Code");
                    if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
                        throw new SsReject("InvalidCourseCode");
                    if (row.Int("LocationId") is not null || row.Text("SubCode") is not null)
                        throw new SsReject("UnsupportedScopedCourse");
                    if (rows["LookupCode"].Count(x => x.Int("Type") == 6 && x.Text("Code") == code) != 1)
                        throw new SsReject("DuplicateCourseCode");
                    var description = row.Text("Description") ?? "";
                    if (description.Length > 200)
                        throw new SsReject("CourseDescriptionTooLong");
                    var validity = row.Int("ValidityPeriod") ?? 0;
                    var notice = row.Int("AdvanceNotice") ?? 0;
                    if (validity < 0 || notice < 0)
                        throw new SsReject("NegativeTrainingPeriod");
                    int? days =
                        validity == 0 ? null
                        : validity == 1461 ? 1460
                        : Math.Min(validity, 1825);
                    if (notice > 0 && days is not null && notice >= days)
                        throw new SsReject("InvalidAdvanceNotice");
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            code,
                            days,
                            notice,
                            Mandatory = row.Bool("Mandatory"),
                            Rotating = row.Bool("Rotating"),
                            Effective = row.Date("EffectiveDate"),
                            Expiry = row.Date("ExpiryDate"),
                            Category = row.Text("Category"),
                        }
                    );
                    if (await session.ReplayAsync("Training", row, hash, true, ct))
                        return;
                    await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Trainings\" IN SHARE ROW EXCLUSIVE MODE", ct);
                    var targets = await db.Trainings.Where(x => x.Code == code).ToListAsync(ct);
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousCourse");
                    var target = targets.SingleOrDefault();
                    var created = target is null;
                    if (target is null)
                    {
                        var category = row.Text("Category");
                        int? categoryId = null;
                        if (!string.IsNullOrWhiteSpace(category))
                        {
                            if (category is not ("Provincial Standard" or "Branch" or "Local") || category.Length > 100)
                                throw new SsReject("UnapprovedTrainingCategory");
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
                                Description = description,
                                EffectiveDate = row.Date("EffectiveDate") ?? session.Now,
                                ExpiryDate = row.Date("ExpiryDate"),
                                Mandatory = row.Bool("Mandatory"),
                                Rotating = row.Bool("Rotating") || validity > 0 || repeats.Contains(row.Int("Id")),
                                ValidityDays = days,
                                AdvanceNoticeDays = notice == 0 ? null : notice,
                                TrainingCategoryId = categoryId,
                                Order = max is null ? 0 : max.Value + 1,
                            }
                        );
                        db.Trainings.Add(target);
                        await db.SaveChangesAsync(ct);
                        if (CarbineCourses.Contains(code))
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
                    await session.LinkAsync("Training", row, "Training", target.Id, hash, created, ct);
                },
                ct
            );
        foreach (
            var row in rows["SheriffTraining"]
                .OrderBy(x => x.Date("EndDate"))
                .ThenBy(x => x.Date("StartDate"))
                .ThenBy(x => x.Int("Id"))
        )
            await session.RowAsync(
                "SheriffTraining",
                row,
                async () =>
                {
                    if (row.Date("EndDate") < boundary)
                    {
                        session.Outcome("SheriffTraining", row.Id, "Excluded", "BeforeHistoryBoundary");
                        return;
                    }
                    var sourceUser = row.Guid("SheriffId")?.ToString() ?? throw new SsReject("MissingUser");
                    if (!users.TryGetValue(sourceUser, out var sheriff))
                        throw new SsReject("MissingSourceUser");
                    if (row.Date("ExpiryDate") is not null || sheriff.Bool("Excused"))
                    {
                        var linked = await db.DataMigrationRecords.AnyAsync(
                            x => x.Source == "SS" && x.EntityType == "SheriffTraining" && x.LegacyId == row.Id,
                            ct
                        );
                        session.Outcome(
                            "SheriffTraining",
                            row.Id,
                            "Excluded",
                            linked ? "PreviouslyMigratedNowExcludedRetained"
                                : sheriff.Bool("Excused") ? "ExcusedSheriff"
                                : "DeletedHistory"
                        );
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    SsWriteSession.Timezone(row, false); // Target UserTraining has no timezone column; report the loss.
                    var hash = SsWriteSession.Hash(
                        new
                        {
                            User = sourceUser,
                            Course = row.Int("TrainingTypeId"),
                            dates,
                            Expiry = row.Date("TrainingCertificationExpiry"),
                            Notice = row.Bool("FirstNotice"),
                        }
                    );
                    if (
                        await session.ReplayAsync(
                            "SheriffTraining",
                            row,
                            hash,
                            false,
                            ct,
                            async () =>
                            {
                                var id = int.Parse(await session.ResolveAsync("SheriffTraining", row.Id, ct));
                                var owned = await db
                                    .UserTrainings.Include(x => x.Training)
                                    .SingleAsync(x => x.Id == id, ct);
                                var resolvedUser = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
                                var resolvedCourse =
                                    await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct)
                                    ?? throw new SsReject("MissingTraining");
                                if (owned.UserId != resolvedUser || owned.TrainingId != resolvedCourse)
                                    throw new SsReject("TrainingHistoryIdentityChangedRequiresReview");
                                owned.AwardedOn = dates.Start;
                                owned.EndingOn = dates.End;
                                owned.ExpiryDate = row.Date("TrainingCertificationExpiry");
                                owned.Notes = MappedNotes(row, owned.Training);
                                owned.NoticeState = row.Bool("FirstNotice")
                                    ? UserTrainingNoticeStates.Sent
                                    : UserTrainingNoticeStates.None;
                                owned.UpdatedOn = session.Now;
                                // Keep the existing version; never invoke renewal or mail services.
                            }
                        )
                    )
                        return;
                    var userId = Guid.Parse(await session.ResolveAsync("User", sourceUser, ct));
                    var courseId =
                        await session.ResolveIntAsync("Training", row.Int("TrainingTypeId"), ct)
                        ?? throw new SsReject("MissingTraining");
                    var course = await db.Trainings.SingleAsync(x => x.Id == courseId, ct);
                    var notes = MappedNotes(row, course);
                    await db.Database.ExecuteSqlRawAsync(
                        "LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE",
                        ct
                    );
                    var histories = await db
                        .UserTrainings.Where(x => x.UserId == userId && x.TrainingId == courseId)
                        .ToListAsync(ct);
                    var max = histories.Select(x => (int?)x.Version).Max();
                    if (max == int.MaxValue)
                        throw new SsReject("VersionOverflow");
                    var target = session.Stamp(
                        new UserTraining
                        {
                            UserId = userId,
                            TrainingId = courseId,
                            Version = max is null ? 1 : max.Value + 1,
                            AwardedOn = dates.Start,
                            EndingOn = dates.End,
                            AllDay = false,
                            ExpiryDate = row.Date("TrainingCertificationExpiry"),
                            Notes = notes,
                            NoticeState = row.Bool("FirstNotice")
                                ? UserTrainingNoticeStates.Sent
                                : UserTrainingNoticeStates.None,
                        }
                    );
                    db.UserTrainings.Add(target);
                    // Only Training owns this user field; do not emit user-created/renewal/email signals.
                    if (course.Code.Contains("Carbine", StringComparison.Ordinal))
                    {
                        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
                        if (user.TrainingProfileId is null)
                        {
                            user.TrainingProfileId = (await CarbineProfileAsync(session.Now, ct)).Id;
                            user.UpdatedOn = session.Now;
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
                        row.Text("Timezone") is not null ? "TargetTrainingTimezoneUnavailable"
                            : older ? "LateOlderHistoryAppended"
                            : "TargetTrainingAllDayNotInSource"
                    );
                },
                ct
            );
        foreach (var row in rows["SheriffStandardTraining"])
            session.Outcome("SheriffStandardTraining", row.Id, "Excluded", "UnifiedMandatoryPolicyReplacesFutureRules");
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
