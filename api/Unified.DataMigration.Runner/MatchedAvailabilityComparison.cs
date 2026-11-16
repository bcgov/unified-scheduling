using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.LoanOut.Models.Calendar;
using Unified.LoanOut.Services;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

internal static class MatchedAvailabilityComparison
{
    internal static async Task<int> ExecuteAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        if (
            db.Database.GetDbConnection().Database
            is not ("unified_ss_rehearsal_ready_v4_rollback" or "unified_ss_rehearsal_ready_v4_probe")
        )
            throw new InvalidOperationException("Matched availability requires the isolated ready-v4 target.");
        var root = Path.GetFullPath("docker/migration/artifacts");
        var sourceName =
            config["DataMigrationRehearsal:AvailabilitySourceArtifactName"] ?? "ready-v4-availability-v1-e6e674c.json";
        var expected = sourceName switch
        {
            "ready-v4-availability-v1-e6e674c.json" => (
                Database: "ss_ready_v4",
                Digest: "002C5DB06A157EB1A79413608E66C0E437FFC362C1DB194B4D8E90DF1BB88D24"
            ),
            "probe-v4-availability-v1-e6e674c.json" => (
                Database: "ss_ready_v4_probe",
                Digest: "874D6B412686D940517685C0A3D4BF73F5A3D866AE11B19E55CF70D0051EB9F6"
            ),
            _ => throw new InvalidOperationException("Unsupported independent availability capture."),
        };
        var input = Path.Combine(root, sourceName);
        var outputName =
            config["DataMigrationRehearsal:MatchedAvailabilityArtifactName"]
            ?? "target-matched-availability-ready-v4-v2.json";
        if (!System.Text.RegularExpressions.Regex.IsMatch(outputName, @"^[A-Za-z0-9._-]+\.json$"))
            throw new InvalidOperationException("Availability artifact must be a direct restricted JSON filename.");
        var output = Path.Combine(root, outputName);
        if (
            File.Exists(output)
            || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)
            || File.GetAttributes(input).HasFlag(FileAttributes.ReparsePoint)
        )
            throw new InvalidOperationException("Unsafe or already retained availability artifact.");
        var bytes = await File.ReadAllBytesAsync(input);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (digest != expected.Digest)
            throw new InvalidOperationException("Independent availability capture digest mismatch.");
        var source = JsonNode.Parse(bytes)!.AsObject();
        if (
            source["Format"]!.GetValue<string>() != "ss-synthetic-availability-oracle/v1"
            || source["SourceDatabase"]!.GetValue<string>() != expected.Database
        )
            throw new InvalidOperationException("Wrong independent availability source.");
        await using var connection = new Npgsql.NpgsqlConnection(config["DataMigration:Sources:SS:ConnectionString"]);
        await connection.OpenAsync();
        await using var sourceTx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var readOnly = new Npgsql.NpgsqlCommand("SET TRANSACTION READ ONLY", connection, sourceTx))
            await readOnly.ExecuteNonQueryAsync();
        await using var marker = new Npgsql.NpgsqlCommand(
            "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database()",
            connection,
            sourceTx
        );
        if (connection.Database != expected.Database || (string?)await marker.ExecuteScalarAsync() != "SS SYNTHETIC")
            throw new InvalidOperationException("Actual source selection/marker mismatch.");
        await sourceTx.RollbackAsync();
        var guard = services.GetRequiredService<ShiftLoanOutAvailabilityService>();
        if (!guard.IsEnabled)
            throw new InvalidOperationException("Actual LoanOut must be enabled.");
        var userService = services.GetRequiredService<IUserService>();
        var loans = services.GetRequiredService<ILoanOutCalendarDataProvider>();
        var conflicts = services.GetRequiredService<ICalendarConflictService>();
        var time = services.GetRequiredService<ITimeZoneService>();
        var resolver = services.GetRequiredService<ICalendarTimeZoneResolver>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        var links = await db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS").ToListAsync();
        var sourceUser = source["SheriffId"]!.GetValue<string>();
        var user = Guid.Parse(links.Single(x => x.EntityType == "User" && x.LegacyId == sourceUser).TargetKey);
        var results = new List<object>();
        var presenceDifferences = 0;
        var conflictCountDifferences = 0;
        var conflictIntervalDifferences = 0;
        var totalSourceConflicts = 0;
        var totalTargetConflicts = 0;
        foreach (var item in source["Results"]!.AsArray())
        {
            var sourceLocation = item!["LocationId"]!.ToString();
            var location = int.Parse(
                links.Single(x => x.EntityType == "Location" && x.LegacyId == sourceLocation).TargetKey
            );
            var targetLocation = await db.Locations.AsNoTracking().SingleAsync(x => x.Id == location);
            var zone = resolver.Resolve(null, targetLocation.Timezone);
            var start = DateTimeOffset.Parse(item["Start"]!.GetValue<string>());
            var end = DateTimeOffset.Parse(item["End"]!.GetValue<string>());
            var from = DateOnly.FromDateTime(time.ToLocalUnspecified(start, zone));
            var through = DateOnly.FromDateTime(time.ToLocalUnspecified(end.AddTicks(-1), zone));
            // These are the actual home-user and borrowed-user read endpoints used
            // by the scheduler's resource contributions. Their date granularity is
            // explicit; no invented instant-filtering of returned users is applied.
            var home = await userService.GetAllAsync(new UserQueryParams { LocationId = location });
            var borrowed = await loans.GetUsersAsync(
                new LoanOutUserSearchRequest
                {
                    LocationId = location,
                    StartDate = from,
                    EndDate = through,
                    TimeZoneId = zone.Id,
                }
            );
            var homePresent = home.Any(x => x.Id == user);
            var borrowedPresent = borrowed.Any(x => x.Id == user);
            var present = homePresent || borrowedPresent;
            var filtered = await guard.GetAssignableUserIdsByOccurrenceAsync(
                [user],
                [new ShiftAssignmentLoanOutContext(start, end)],
                location,
                zone.Id
            );
            var actualConflicts = await conflicts.GetConflictsForCandidatesAsync([
                new CalendarConflictParticipant(
                    "ss-migration-availability-proposal",
                    "scheduling",
                    user,
                    start,
                    end,
                    "Migration availability probe",
                    TimeZoneId: zone.Id,
                    EventTypeCode: "shift"
                ),
            ]);
            var calendar = await loans.GetDataAsync(
                new SchedulingCalendarEventQueryContext
                {
                    StartAtUtc = start,
                    EndAtUtc = end,
                    LocationId = location,
                    UserIds = [user],
                }
            );
            var sConflicts = item["Conflicts"]!.AsArray();
            var sourcePresent = item["SubjectPresent"]!.GetValue<bool>();
            if (sourcePresent != present)
                presenceDifferences++;
            if (sConflicts.Count != actualConflicts.Count)
                conflictCountDifferences++;
            var sourceIntervals = sConflicts
                .Select(x =>
                    (
                        Start: DateTimeOffset.Parse(x!["Start"]!.GetValue<string>()).ToUniversalTime(),
                        End: DateTimeOffset.Parse(x["End"]!.GetValue<string>()).ToUniversalTime()
                    )
                )
                .OrderBy(x => x.Start)
                .ThenBy(x => x.End);
            var targetIntervals = actualConflicts
                .Select(x => (Start: x.OverlapStart.ToUniversalTime(), End: x.OverlapEnd.ToUniversalTime()))
                .OrderBy(x => x.Start)
                .ThenBy(x => x.End);
            if (!sourceIntervals.SequenceEqual(targetIntervals))
                conflictIntervalDifferences++;
            totalSourceConflicts += sConflicts.Count;
            totalTargetConflicts += actualConflicts.Count;
            results.Add(
                new
                {
                    Probe = item["Probe"]!.GetValue<string>(),
                    SourceLocationId = sourceLocation,
                    SourceUserId = sourceUser,
                    Start = start,
                    End = end,
                    TargetTimeZoneId = zone.Id,
                    SourceSubjectPresent = sourcePresent,
                    SourceConflicts = sConflicts.DeepClone(),
                    TargetHomeUserEndpointPresent = homePresent,
                    TargetBorrowedUserEndpointPresent = borrowedPresent,
                    TargetResourceEndpointUnionPresent = present,
                    TargetUserEndpointDateRange = new { StartDate = from, EndDate = through },
                    TargetExplicitCandidatePassesLoanOutGuard = filtered.Single().Contains(user),
                    TargetProposedShiftConflicts = actualConflicts
                        .Select(x => new
                        {
                            x.OverlapStart,
                            x.OverlapEnd,
                            x.IsOverridden,
                            EntryModule = x.Entry.SourceModule,
                            EntryType = x.Entry.EventTypeCode,
                            EntryStart = x.Entry.Start,
                            EntryEnd = x.Entry.End,
                            OtherModule = x.Overlaps.SourceModule,
                            OtherType = x.Overlaps.EventTypeCode,
                            OtherStart = x.Overlaps.Start,
                            OtherEnd = x.Overlaps.End,
                        })
                        .ToArray(),
                    TargetLoanOutCalendarIntervals = calendar
                        .Select(x => new
                        {
                            x.Start,
                            x.End,
                            x.TimeZoneId,
                        })
                        .ToArray(),
                }
            );
        }
        await tx.RollbackAsync();
        var summary = new
        {
            Verdict = presenceDifferences > 0 || conflictCountDifferences > 0 || conflictIntervalDifferences > 0
                ? "Fail"
                : "NeedsActionContractReview",
            Calls = results.Count,
            CandidatePresenceDifferentCalls = presenceDifferences,
            ConflictCountDifferentCalls = conflictCountDifferences,
            ConflictOverlapIntervalDifferentCalls = conflictIntervalDifferences,
            SourceConflictInstances = totalSourceConflicts,
            TargetConflictInstances = totalTargetConflicts,
        };
        var envelope = new
        {
            Format = "unified-ss-matched-availability/v1",
            summary.Verdict,
            SourceArtifact = Path.GetFileName(input),
            SourceArtifactSha256 = digest,
            SourceDatabase = connection.Database,
            SourceRevision = source["SourceRevision"]!.GetValue<string>(),
            TargetDatabase = db.Database.GetDbConnection().Database,
            ActualServices = new[]
            {
                nameof(UserService),
                nameof(LoanOutCalendarDataProvider),
                nameof(ShiftLoanOutAvailabilityService),
                nameof(CalendarConflictService),
            },
            TargetConflictRules = config.GetSection("CalendarConflicts:Rules").Get<Dictionary<string, string[]>>(),
            Provenance = "Actual read-only service calls. Source ShiftService.GetShiftAvailability vs target resource endpoints, proposed-shift conflict engine, LoanOut calendar provider and candidate guard.",
            SemanticLimits = new[]
            {
                "User discovery endpoints accept dates, not SS instant windows.",
                "Proposal conflict check is not a persisted shift or executed UI.",
                "Candidate guard is not candidate discovery; calendar events are not conflict responses.",
                "Source complete conflict intervals and target overlap intervals are separate response contracts.",
                "No free minutes, SS formulas, source statuses or UI projections reconstructed.",
            },
            Summary = summary,
            Results = results,
        };
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var file = new FileStream(output, options);
        await JsonSerializer.SerializeAsync(file, envelope, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(JsonSerializer.Serialize(summary));
        // This diagnostic never grants business acceptance across different service
        // contracts. Measured differences fail; equal observations still need review.
        return summary.Verdict == "Fail" ? 1 : 2;
    }
}
