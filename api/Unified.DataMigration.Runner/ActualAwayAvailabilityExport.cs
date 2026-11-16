using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

// Actual target assignability responses, not an SS conflict/status reconstruction.
internal static class ActualAwayAvailabilityExport
{
    internal static async Task ExecuteAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        if (
            db.Database.GetDbConnection().Database
            is not (
                "unified_ss_rehearsal_ready_v4"
                or "unified_ss_rehearsal_ready_v4_recovery"
                or "unified_ss_rehearsal_ready_v4_probe"
                or "unified_ss_rehearsal_ready_v4_rollback"
            )
        )
            throw new InvalidOperationException("Availability probe is isolated ready-v4 rehearsal only.");
        await using var source = new Npgsql.NpgsqlConnection(config["DataMigration:Sources:SS:ConnectionString"]);
        await source.OpenAsync();
        await using var sourceTx = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var readOnly = new Npgsql.NpgsqlCommand("SET TRANSACTION READ ONLY", source, sourceTx))
            await readOnly.ExecuteNonQueryAsync();
        await using var marker = new Npgsql.NpgsqlCommand(
            "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database()",
            source,
            sourceTx
        );
        if (
            source.Database is not ("ss_ready_v4" or "ss_ready_v4_probe")
            || (string?)await marker.ExecuteScalarAsync() != "SS SYNTHETIC"
        )
            throw new InvalidOperationException("Explicit ready-v4 synthetic source marker is required.");
        await sourceTx.RollbackAsync();
        var service = services.GetRequiredService<ShiftLoanOutAvailabilityService>();
        if (!service.IsEnabled)
            throw new InvalidOperationException("Availability probe requires LoanOut enabled.");
        var sourceAway = config["DataMigrationRehearsal:SourceAwayId"] ?? "900000";
        var filename =
            config["DataMigrationRehearsal:AvailabilityArtifactName"]
            ?? $"target-availability-{db.Database.GetDbConnection().Database}.json";
        if (!System.Text.RegularExpressions.Regex.IsMatch(filename, @"^[A-Za-z0-9._-]+\.json$"))
            throw new InvalidOperationException("Availability artifact must be a direct JSON filename.");
        var root = Path.GetFullPath("docker/migration/artifacts");
        if (!Directory.Exists(root) || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Restricted artifact directory is required.");
        var path = Path.Combine(root, filename);
        if (File.Exists(path))
            throw new InvalidOperationException("Never overwrite retained availability evidence.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        var links = await db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS").ToListAsync();
        var link = links.Single(x => x.EntityType == "SheriffAwayLocation" && x.LegacyId == sourceAway);
        var entry = await db
            .LoanOutEntries.AsNoTracking()
            .Include(x => x.Event)
            .Include(x => x.Users)
            .SingleAsync(x => x.Id == int.Parse(link.TargetKey));
        var e = entry.Event!;
        if (
            e.StatusTypeCode == "cancelled"
            || e.EndAtUtc is not { } end
            || e.LocationId is not { } home
            || end <= e.StartAtUtc
            || entry.Users.Count != 1
        )
            throw new InvalidOperationException("Probe requires a live, single-user, positive away interval.");
        var location = await db.Locations.AsNoTracking().SingleAsync(x => x.Id == home);
        var time = services.GetRequiredService<ITimeZoneService>();
        var zone = services.GetRequiredService<ICalendarTimeZoneResolver>().Resolve(null, location.Timezone);
        var firstDay = DateOnly.FromDateTime(time.ToLocalUnspecified(e.StartAtUtc, zone));
        var lastDay = DateOnly.FromDateTime(time.ToLocalUnspecified(end.AddTicks(-1), zone));
        if (firstDay != lastDay)
            throw new InvalidOperationException("This bounded boundary probe requires a single local day.");
        var day = time.ConvertInclusiveLocalDateRangeToUtcRange(firstDay, firstDay, zone);
        var configuredStart = config["DataMigrationRehearsal:AvailabilityStartUtc"];
        var configuredEnd = config["DataMigrationRehearsal:AvailabilityEndUtc"];
        if ((configuredStart is null) != (configuredEnd is null))
            throw new InvalidOperationException("Both explicit UTC probe boundaries are required.");
        var probeStart = configuredStart is null
            ? day.StartAtUtc
            : DateTimeOffset.Parse(configuredStart).ToUniversalTime();
        var probeEnd = configuredEnd is null ? day.EndAtUtc : DateTimeOffset.Parse(configuredEnd).ToUniversalTime();
        if (probeStart >= e.StartAtUtc || probeEnd <= end || probeEnd - probeStart > TimeSpan.FromDays(2))
            throw new InvalidOperationException("Probe boundaries must surround the away within two days.");
        // PostgreSQL instants are microsecond precision. Strict windows must not
        // accidentally touch the source boundary through rounding.
        var windows = new[]
        {
            (Name: "before-boundary-touch", Start: probeStart, End: e.StartAtUtc),
            (Name: "before-strict", Start: probeStart, End: e.StartAtUtc.AddTicks(-10)),
            (Name: "inside", Start: e.StartAtUtc, End: end),
            (Name: "after-boundary-touch", Start: end, End: probeEnd),
            (Name: "after-strict", Start: end.AddTicks(10), End: probeEnd),
            (Name: "whole-probe-range", Start: probeStart, End: probeEnd),
        };
        var user = entry.Users.Single().UserId;
        var sourceUser = links.Single(x => x.EntityType == "User" && x.TargetKey == user.ToString()).LegacyId;
        var rows = new List<object>();
        foreach (
            var destination in new[] { (Name: "home", Id: home), (Name: "destination", Id: entry.LoanOutLocationId) }
        )
        {
            var sourceLocation = links
                .Single(x => x.EntityType == "Location" && x.TargetKey == destination.Id.ToString())
                .LegacyId;
            foreach (var window in windows)
            {
                if (window.End <= window.Start)
                    throw new InvalidOperationException("Away must have positive before/after probe windows.");
                var response = await service.GetAssignableUserIdsByOccurrenceAsync(
                    [user],
                    [new ShiftAssignmentLoanOutContext(window.Start, window.End)],
                    destination.Id,
                    zone.Id,
                    CancellationToken.None
                );
                rows.Add(
                    new
                    {
                        SourceAwayId = sourceAway,
                        SourceUserId = sourceUser,
                        SourceLocationId = sourceLocation,
                        Location = destination.Name,
                        Window = window.Name,
                        Start = window.Start,
                        End = window.End,
                        TimeZoneId = zone.Id,
                        UserAssignable = response.Single().Contains(user),
                    }
                );
            }
        }
        await tx.RollbackAsync();
        var envelope = new
        {
            Format = "unified-ss-away-availability/v1",
            Marker = "SS SYNTHETIC",
            SourceMetadata = new { Database = source.Database, SourceAwayId = sourceAway },
            TargetDatabase = db.Database.GetDbConnection().Database,
            ActualService = nameof(ShiftLoanOutAvailabilityService),
            LoanOutEnabled = service.IsEnabled,
            ExplicitUtcProbeBoundaries = configuredStart is not null,
            HomeLocationTimeZoneId = zone.Id,
            ExplicitSingleCandidateInput = true,
            FullUserDiscoveryExecuted = false,
            SourceServiceExecutedByThisCommand = false,
            ContractsDiffer = "This filters one supplied candidate; destination true is not user discovery. Not SS conflicts or free-minute/UI evidence.",
            Rows = rows,
        };
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var file = new FileStream(path, options);
        await JsonSerializer.SerializeAsync(file, envelope, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(JsonSerializer.Serialize(new { Artifact = filename, Rows = rows.Count }));
    }
}
