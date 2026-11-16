using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Reporting;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Training.Services.Reporting;

// Adapts actual target responses and identities only. Unsupported SS fields/surfaces remain
// null/missing with explicit gaps; neither SS statuses nor OT algorithms are reconstructed.
internal static class NormalizedTargetExport
{
    internal sealed record Row(string Window, string Report, string Key, string Value);

    internal static async Task<bool> ExecuteAsync(
        IServiceProvider services,
        IConfiguration config,
        string? nameOverride = null
    )
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        var root = Path.GetFullPath("docker/migration/artifacts");
        var sourceName = config["DataMigrationRehearsal:SourceArtifactName"] ?? "source-all-v1-9a57c54.json";
        var name =
            nameOverride
            ?? config["DataMigrationRehearsal:TargetArtifactName"]
            ?? $"target-{db.Database.GetDbConnection().Database}.json";
        string Artifact(string file)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(file, @"^[A-Za-z0-9._-]+\.json$"))
                throw new InvalidOperationException("Artifact must be a direct restricted JSON filename.");
            var path = Path.Combine(root, file);
            if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Symlink artifacts are not permitted.");
            return path;
        }
        var sourcePath = Artifact(sourceName);
        if (
            new FileInfo(sourcePath).Length > 20 * 1024 * 1024
            || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)
        )
            throw new InvalidOperationException("Restricted artifact boundary failed.");
        var source = JsonNode.Parse(await File.ReadAllTextAsync(sourcePath))!.AsObject();
        if (
            source["Format"]?.GetValue<string>() != "ss-synthetic-report-oracle/v1"
            || source["Marker"]?.GetValue<string>() != "SS SYNTHETIC"
        )
            throw new InvalidOperationException("Source oracle envelope is not supported.");
        var output = Artifact(name);
        var comparisonPath = Artifact(Path.GetFileNameWithoutExtension(name) + "-comparison.json");
        if (File.Exists(output) || File.Exists(comparisonPath))
            throw new InvalidOperationException("Never overwrite an existing export or comparison.");
        var result = new List<Row>();
        var gaps = new HashSet<string>(StringComparer.Ordinal)
        {
            "TrainingExcusedAndRowVariantNotExposedByTargetReport",
            "TrainingUiProjectionIsNotAnExecutedTargetUiExport",
            "DistributionReadOracleNotExposedByTargetDeliveryCommand",
            "TargetSlotFlagsAndStoredOvertimeAreNotExposed",
            "HistoricalReportInstantVersusCurrentTargetComplianceRequiresReview",
        };
        void Emit(string window, string report, object key, object value) =>
            result.Add(new Row(window, report, JsonSerializer.Serialize(key), JsonSerializer.Serialize(value)));
        string? Instant(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        var counts = source["FixtureCounts"]!.AsObject();
        var sourceRowsForCounts = await services
            .GetRequiredService<Unified.DataMigration.Sources.SsSnapshotReader>()
            .ReadAsync(counts.Select(x => x.Key == "Sheriff" ? "User" : x.Key).Distinct(), 100, CancellationToken.None);
        foreach (var count in counts)
        {
            var actual =
                count.Key == "Sheriff"
                    ? sourceRowsForCounts["User"].Count(x => x.Text("Discriminator") == "Sheriff")
                    : sourceRowsForCounts[count.Key].Count;
            if (actual != count.Value!.GetValue<int>())
                gaps.Add("SourceFixtureDoesNotMatchIndependentOracleCapture");
        }
        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)
            : null;
        if (tx is not null)
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        var links = await db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS").ToListAsync();
        var inverse = links
            .GroupBy(x => (x.EntityType, x.TargetKey))
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.LegacyId, StringComparer.Ordinal).ToList());
        string? Legacy(string type, object? target, bool representative = false)
        {
            if (target is null)
                return null;
            if (
                !inverse.TryGetValue(
                    (type, Convert.ToString(target, CultureInfo.InvariantCulture)!),
                    out var candidates
                )
            )
            {
                gaps.Add("TargetResponseIdentityHasNoCrosswalk");
                return null;
            }
            if (candidates.Count > 1)
            {
                gaps.Add(representative ? "MergedTargetRequiresGroupAwareComparison" : "NonUniqueInverseCrosswalk");
                if (!representative)
                    return null;
            }
            return candidates[0].LegacyId;
        }
        int? LegacyInt(string type, object? target, bool representative = false) =>
            int.TryParse(Legacy(type, target, representative), out var id) ? id : null;
        var users = await db
            .Users.AsNoTracking()
            .Include(x => x.HomeLocation)
            .Where(x => x.Id != Unified.Db.Models.UserManagement.User.SystemUser)
            .ToListAsync();
        var locationIds = links.Where(l => l.EntityType == "Location").Select(l => int.Parse(l.TargetKey)).ToList();
        var locations = await db.Locations.AsNoTracking().Where(x => locationIds.Contains(x.Id)).ToListAsync();
        var calendar = services.GetRequiredService<ISchedulingCalendarService>();
        var workingHours = services.GetRequiredService<IWorkingHoursService>();
        var training = new UserTrainingReportQueryHandler(db, TimeProvider.System);
        var latest = await db
            .DataMigrationRuns.AsNoTracking()
            .Where(x => x.Source == "SS" && !x.DryRun && x.Status != "RolledBack")
            .ToListAsync();
        foreach (var job in new[] { "MyTeam", "Training", "Scheduling" })
            if (
                latest.Where(x => x.Job == job).OrderByDescending(x => x.StartedOn).FirstOrDefault()?.Status
                != "Completed"
            )
                gaps.Add("PrimaryAppliedJobsNotCompleted");
        foreach (var window in source["Windows"]!.AsArray())
        {
            var windowName = window!["Name"]!.GetValue<string>();
            var start = DateTimeOffset.Parse(window["Start"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var end = DateTimeOffset.Parse(window["End"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var from = DateOnly.FromDateTime(start.DateTime);
            var through = DateOnly.FromDateTime(end.DateTime).AddDays(-1);
            foreach (var user in users)
            {
                var sheriff = Legacy("User", user.Id);
                if (sheriff is null)
                    continue;
                var response = (UserTrainingReportResponse)
                    await training.ExecuteAsync(
                        new Dictionary<string, IReadOnlyCollection<string>>
                        {
                            ["userId"] = [user.Id.ToString()],
                            ["endDate"] = [through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
                        },
                        null,
                        SortDirection.Asc
                    );
                foreach (var item in response.Rows)
                    Emit(
                        windowName,
                        "training-api",
                        new { sheriffId = sheriff, courseId = LegacyInt("Training", item.TrainingId) },
                        new
                        {
                            locationId = LegacyInt("Location", user.HomeLocationId),
                            regionId = LegacyInt("Region", user.HomeLocation?.RegionId),
                            end = Instant(item.EndingOn),
                            expiry = Instant(item.ExpiryDate),
                            excused = (bool?)null,
                            status = item.Status,
                            rowVariant = (string?)null,
                        }
                    );
            }
            var hours = await workingHours.QueryAsync(new WorkingHoursQuery { StartDate = from, EndDate = through });
            foreach (var location in locations)
            {
                var sourceLocation = LegacyInt("Location", location.Id);
                if (sourceLocation is null)
                    continue;
                var response = await calendar.GetDataAsync(
                    new SchedulingCalendarRequest
                    {
                        StartDate = from,
                        EndDate = through,
                        LocationId = location.Id,
                    },
                    true,
                    true,
                    true,
                    true
                );
                var events = response.Events.Where(x => x.Start < end && (x.End ?? x.Start) >= start).ToList();
                foreach (var shift in events.Where(x => x.ShiftEntryId != null))
                {
                    var sourceShift = LegacyInt("Shift", shift.ShiftEntryId, true);
                    foreach (var user in shift.UserIds)
                        Emit(
                            windowName,
                            "shifts",
                            new { locationId = sourceLocation, shiftId = sourceShift },
                            new
                            {
                                sheriffId = Legacy("User", user),
                                start = Instant(shift.Start),
                                end = Instant(shift.End),
                                timezone = shift.TimeZoneId,
                                anticipatedAssignmentId = (int?)null,
                                workSection = (string?)null,
                                overtime = (decimal?)null,
                                slotIds = (int[]?)null,
                            }
                        );
                    foreach (var item in hours.Where(x => x.ShiftEntryId == shift.ShiftEntryId))
                        Emit(
                            windowName,
                            "overtime-allocation",
                            new { locationId = sourceLocation, shiftId = sourceShift },
                            new { hours = item.OvertimeMinutes / 60m }
                        );
                }
                foreach (var duty in events.Where(x => x.AssignmentEntryId != null))
                {
                    var sourceDuty = LegacyInt("Duty", duty.AssignmentEntryId);
                    if (sourceDuty is null)
                        continue;
                    var sourceSlots = links
                        .Where(x =>
                            x.EntityType == "DutySlot"
                            && x.TargetKey == duty.AssignmentEntryId!.Value.ToString(CultureInfo.InvariantCulture)
                        )
                        .Select(x => int.Parse(x.LegacyId, CultureInfo.InvariantCulture))
                        .OrderBy(x => x)
                        .ToArray();
                    Emit(
                        windowName,
                        "duties",
                        new { locationId = sourceLocation, dutyId = sourceDuty },
                        new
                        {
                            assignmentId = LegacyInt("Assignment", duty.AssignmentDefinitionId, true),
                            start = Instant(duty.Start),
                            end = Instant(duty.End),
                            timezone = duty.TimeZoneId,
                            slotIds = sourceSlots,
                        }
                    );
                    if (sourceSlots.Length == 1 && duty.AssignedUserIds.Count <= 1)
                        Emit(
                            windowName,
                            "duty-slots",
                            new
                            {
                                locationId = sourceLocation,
                                dutyId = sourceDuty,
                                slotId = sourceSlots[0],
                            },
                            new
                            {
                                sheriffId = duty.AssignedUserIds.Count == 0
                                    ? null
                                    : Legacy("User", duty.AssignedUserIds.Single()),
                                start = Instant(duty.Start),
                                end = Instant(duty.End),
                                timezone = duty.TimeZoneId,
                                notRequired = duty.Capacity == 0,
                                notAvailable = (bool?)null,
                                overtime = (bool?)null,
                                closed = (bool?)null,
                            }
                        );
                    else if (sourceSlots.Length > 0)
                        gaps.Add("MergedSlotRepresentationCannotBeExpandedIntoInventedTargetRows");
                }
                var locationShiftIds = events
                    .Where(x => x.ShiftEntryId != null)
                    .Select(x => x.ShiftEntryId!.Value)
                    .ToHashSet();
                var presentDays = hours
                    .Where(x => locationShiftIds.Contains(x.ShiftEntryId))
                    .Select(x => (x.UserId, x.Date))
                    .Distinct();
                foreach (var (user, day) in presentDays)
                    Emit(
                        windowName,
                        "overtime-calculated",
                        new
                        {
                            locationId = sourceLocation,
                            sheriffId = Legacy("User", user),
                            localDay = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            timezone = location.Timezone,
                        },
                        new
                        {
                            hours = hours.Where(x => x.UserId == user && x.Date == day).Sum(x => x.OvertimeMinutes)
                                / 60m,
                        }
                    );
            }
        }
        if (tx is not null)
            await tx.RollbackAsync();
        var target = new JsonObject();
        foreach (var property in source.Where(x => x.Key != "Rows"))
            target[property.Key] = property.Value?.DeepClone();
        target["Rows"] = JsonSerializer.SerializeToNode(result);
        target["TargetEvidence"] = JsonSerializer.SerializeToNode(
            new
            {
                Database = db.Database.GetDbConnection().Database,
                CurrentSourceDatabase = new Npgsql.NpgsqlConnectionStringBuilder(
                    config["DataMigration:Sources:SS:ConnectionString"]
                ).Database,
                ActualServices = new[]
                {
                    "UserTrainingReportQueryHandler",
                    "SchedulingCalendarService",
                    "WorkingHoursService",
                },
                TargetWorkingDayMinutes = config.GetValue<int>("WorkingHours:FullWorkingDayMinutes"),
                Gaps = gaps.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                UiCsvPdfExecuted = false,
            }
        );
        var sourceRows = source["Rows"]!.Deserialize<List<Row>>()!;
        string Canonical(string text)
        {
            JsonNode? Sort(JsonNode? node) =>
                node switch
                {
                    JsonObject obj => new JsonObject(
                        obj.OrderBy(x => x.Key, StringComparer.Ordinal)
                            .Select(x => KeyValuePair.Create(x.Key, Sort(x.Value)))
                            .ToArray()
                    ),
                    JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
                    _ => node?.DeepClone(),
                };
            return Sort(JsonNode.Parse(text))!.ToJsonString();
        }
        string Identity(Row row) => $"{row.Window}\n{row.Report}\n{Canonical(row.Key)}\n{Canonical(row.Value)}";
        var sourceCounts = sourceRows
            .GroupBy(Identity)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var targetCounts = result.GroupBy(Identity).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var missing = sourceCounts.Sum(x => Math.Max(0, x.Value - targetCounts.GetValueOrDefault(x.Key)));
        var unexpected = targetCounts.Sum(x => Math.Max(0, x.Value - sourceCounts.GetValueOrDefault(x.Key)));
        var comparison = new
        {
            Verdict = missing == 0 && unexpected == 0 && gaps.Count == 0 ? "Pass" : "Fail",
            SourceArtifact = sourceName,
            TargetArtifact = name,
            SourceRows = sourceRows.Count,
            TargetRows = result.Count,
            MissingRows = missing,
            UnexpectedRows = unexpected,
            Gaps = gaps.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            BySurface = sourceRows
                .Concat(result)
                .Select(x => (x.Window, x.Report))
                .Distinct()
                .OrderBy(x => x.Window)
                .ThenBy(x => x.Report)
                .Select(x => new
                {
                    x.Window,
                    x.Report,
                    SourceRows = sourceRows.Count(r => r.Window == x.Window && r.Report == x.Report),
                    TargetRows = result.Count(r => r.Window == x.Window && r.Report == x.Report),
                })
                .ToArray(),
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        async Task WriteNew(string path, string content)
        {
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var file = new FileStream(path, fileOptions);
            await using var writer = new StreamWriter(file);
            await writer.WriteAsync(content);
        }
        await WriteNew(output, target.ToJsonString(options));
        await WriteNew(comparisonPath, JsonSerializer.Serialize(comparison, options));
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    comparison.Verdict,
                    comparison.SourceRows,
                    comparison.TargetRows,
                    comparison.MissingRows,
                    comparison.UnexpectedRows,
                    GapCount = gaps.Count,
                }
            )
        );
        return comparison.Verdict == "Pass";
    }
}
