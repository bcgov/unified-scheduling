using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.DataMigration.Services;

public sealed class SsTrainingWriter(UnifiedDbContext db)
{
    public static readonly string[] Tables = ["LookupCode", "User", "SheriffTraining", "SheriffStandardTraining"];

    internal static string AllocateCourseCode(string code, IReadOnlySet<string> reserved) =>
        SsTrainingRules.AllocateCourseCode(code, reserved);

    public async Task WriteAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        var users = rows["User"].ToDictionary(x => x.Id);
        var sourceCodes = SsTrainingRules.SourceCourseCodes(rows["LookupCode"]);
        var shortenedCarbineCourseIds = SsTrainingRules.ShortenedCarbineCourseIds(rows["LookupCode"]);
        var courseCodeCounts = SsTrainingRules.CourseCodeCounts(rows["LookupCode"]);
        var repeats = SsTrainingRules.RepeatedCourses(rows["SheriffTraining"], users, boundary);
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
        foreach (var row in SsTrainingRules.HistoryOrder(rows["SheriffTraining"]))
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
                            SsTrainingRules.ExcludedHistoryReason(linked, sheriff.Bool("Excused"))
                        );
                        return;
                    }
                    var dates = SsWriteSession.Dates(row);
                    SsWriteSession.Timezone(row, false); // Target UserTraining has no timezone column; report the loss.
                    var hash = SsTrainingRules.HistoryHash(row, sourceUser, dates);
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
                                owned.AwardedOn = dates.Start;
                                owned.EndingOn = dates.End;
                                owned.ExpiryDate = row.Date("TrainingCertificationExpiry");
                                owned.Notes = SsTrainingRules.MappedNotes(row, owned.Training.Code);
                                owned.NoticeState = SsTrainingRules.NoticeState(row);
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
                    var notes = SsTrainingRules.MappedNotes(row, course.Code);
                    await db.Database.ExecuteSqlRawAsync(
                        "LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE",
                        ct
                    );
                    var histories = await db
                        .UserTrainings.Where(x => x.UserId == userId && x.TrainingId == courseId)
                        .ToListAsync(ct);
                    var version = SsTrainingRules.NextVersion(histories.Select(x => (int?)x.Version).Max());
                    var target = session.Stamp(
                        new UserTraining
                        {
                            UserId = userId,
                            TrainingId = courseId,
                            Version = version,
                            AwardedOn = dates.Start,
                            EndingOn = dates.End,
                            AllDay = false,
                            ExpiryDate = row.Date("TrainingCertificationExpiry"),
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
