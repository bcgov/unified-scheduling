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
using Unified.Training.Models.Calendar;
using Unified.Training.Services;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

// Read actual resource/calendar APIs. This is NOT a replacement SS distribution algorithm,
// target mail/PDF delivery, or a full-field equality claim for the SS distribution DTO.
internal static class ActualReportSurfaces
{
    internal static string Step { get; private set; } = "preflight";

    private sealed record Observation(
        string Window,
        string Surface,
        string Location,
        string? User,
        string Kind,
        DateTimeOffset? Start,
        DateTimeOffset? End
    );

    internal static async Task<bool> ExecuteAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        if (
            db.Database.GetDbConnection().Database != "unified_ss_rehearsal_ready_v4_rollback"
            || new Npgsql.NpgsqlConnectionStringBuilder(config["DataMigration:Sources:SS:ConnectionString"]).Database
                != "ss_ready_v4"
        )
            throw new InvalidOperationException("Exact matched ready-v4 source/target required.");
        var root = Path.GetFullPath("docker/migration/artifacts");
        var input = Path.Combine(root, "ready-v4-source-all-v1-80e9aae.json");
        var output = Path.Combine(root, "target-report-surfaces-v12.json");
        if (
            File.Exists(output)
            || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)
            || File.GetAttributes(input).HasFlag(FileAttributes.ReparsePoint)
        )
            throw new InvalidOperationException("Unsafe or already retained report evidence.");
        var bytes = await File.ReadAllBytesAsync(input);
        if (
            Convert.ToHexString(SHA256.HashData(bytes))
            != "DC28370841D1B444D3950E897EDEE2AA268ACC1EDAD7DFDCCDEECA2B1EDC62C7"
        )
            throw new InvalidOperationException("Matched source digest differs.");
        var source = JsonNode.Parse(bytes)!;
        var people = services.GetRequiredService<IUserService>();
        var loans = services.GetRequiredService<ILoanOutCalendarDataProvider>();
        var calendar = services.GetRequiredService<ISchedulingCalendarService>();
        var conflicts = services.GetRequiredService<ICalendarConflictService>();
        var training = new TrainingCalendarEventService(db);
        var time = services.GetRequiredService<ITimeZoneService>();
        var resolver = services.GetRequiredService<ICalendarTimeZoneResolver>();
        if (
            new Npgsql.NpgsqlConnectionStringBuilder(config["DatabaseConnectionString"]).Options
            != "-c default_transaction_read_only=on"
        )
            throw new InvalidOperationException("Every endpoint connection must default to read-only.");
        await db.Database.OpenConnectionAsync();
        using (var guard = db.Database.GetDbConnection().CreateCommand())
        {
            guard.CommandText = "SELECT current_setting('transaction_read_only')";
            if ((string?)await guard.ExecuteScalarAsync() != "on")
                throw new InvalidOperationException("Actual target connection is not read-only.");
        }
        var links = await db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS").ToListAsync();
        string? User(Guid id) =>
            links.SingleOrDefault(x => x.EntityType == "User" && x.TargetKey == id.ToString())?.LegacyId;
        var observations = new List<Observation>();
        var calls = new List<object>();
        var failures = new List<object>();
        foreach (var window in source["Windows"]!.AsArray())
        {
            var name = window!["Name"]!.GetValue<string>();
            // Npgsql timestamptz parameters require UTC offsets. Preserve the same
            // instants from the independent window; do not shift dates or invent SS logic.
            var start = DateTimeOffset.Parse(window["Start"]!.GetValue<string>()).ToUniversalTime();
            var end = DateTimeOffset.Parse(window["End"]!.GetValue<string>()).ToUniversalTime();
            foreach (var link in links.Where(x => x.EntityType == "Location"))
            {
                var location = await db.Locations.AsNoTracking().SingleAsync(x => x.Id == int.Parse(link.TargetKey));
                var zone = resolver.Resolve(null, location.Timezone);
                var from = DateOnly.FromDateTime(time.ToLocalUnspecified(start, zone));
                var through = DateOnly.FromDateTime(time.ToLocalUnspecified(end.AddTicks(-1), zone));
                Step = "home-resource-service";
                var home = await people.GetAllAsync(new UserQueryParams { LocationId = location.Id });
                Step = "borrowed-resource-service";
                var borrowed = await loans.GetUsersAsync(
                    new LoanOutUserSearchRequest
                    {
                        LocationId = location.Id,
                        StartDate = from,
                        EndDate = through,
                        TimeZoneId = zone.Id,
                    }
                );
                foreach (var id in home.Select(x => x.Id).Concat(borrowed.Select(x => x.Id)).Distinct())
                    observations.Add(new(name, "resources", link.LegacyId, User(id), "user", null, null));
                Step = "scheduling-calendar-service";
                var events = await calendar.GetDataAsync(
                    new SchedulingCalendarRequest
                    {
                        LocationId = location.Id,
                        StartDate = from,
                        EndDate = through,
                    },
                    true,
                    true,
                    true,
                    true
                );
                foreach (var item in events.Events.Where(x => x.Start < end && (x.End ?? x.Start) >= start))
                {
                    var kind =
                        item.ShiftEntryId != null ? "shift"
                        : item.LeaveEntryId != null ? "leave"
                        : item.LoanOutEntryId != null ? "loanOut"
                        : null;
                    if (kind is null)
                        continue; // Assignment DTO is not an SS resource-availability event.
                    foreach (var id in item.UserIds)
                        observations.Add(
                            new(name, "availability-events", link.LegacyId, User(id), kind, item.Start, item.End)
                        );
                }
                Step = "training-calendar-service";
                var courses = await training.GetEventsAsync(
                    new TrainingCalendarEventsRequest
                    {
                        LocationId = location.Id,
                        StartDate = from,
                        EndDate = through,
                        TimeZoneId = zone.Id,
                    }
                );
                foreach (var item in courses.Where(x => x.Start < end && x.End >= start))
                    observations.Add(
                        new(
                            name,
                            "availability-events",
                            link.LegacyId,
                            User(item.UserId),
                            "training",
                            item.Start,
                            item.End
                        )
                    );
                Step = "calendar-conflict-service";
                int? conflictCount = null;
                try
                {
                    var actualConflicts = await conflicts.GetConflictsAsync(
                        new CalendarConflictQuery(start, end, LocationId: location.Id)
                    );
                    conflictCount = actualConflicts.Count;
                }
                catch (ArgumentException error)
                {
                    // Retain a FAILED actual read, never substitute zero or a source formula.
                    // Method metadata is safe; exception messages/keys may contain private values.
                    failures.Add(
                        new
                        {
                            Window = name,
                            SourceLocationId = link.LegacyId,
                            Operation = Step,
                            Failure = error.GetType().Name,
                            Methods = new System.Diagnostics.StackTrace(error)
                                .GetFrames()
                                .Take(8)
                                .Select(x => x.GetMethod()?.DeclaringType?.FullName + "." + x.GetMethod()?.Name)
                                .ToArray(),
                        }
                    );
                }
                calls.Add(
                    new
                    {
                        Window = name,
                        SourceLocationId = link.LegacyId,
                        TimeZone = zone.Id,
                        HomeUsers = home.Count,
                        BorrowedUsers = borrowed.Count,
                        CalendarEvents = events.Events.Count,
                        TrainingEvents = courses.Count,
                        ActualConflictPairs = conflictCount,
                    }
                );
            }
        }
        Step = "normalize-source-common-projection";
        var expected = new List<Observation>();
        foreach (var row in source["Rows"]!.AsArray())
        {
            var report = row!["Report"]!.GetValue<string>();
            if (report is not ("distribution-conflicts" or "distribution-sheriffs"))
                continue;
            var key = JsonNode.Parse(row["Key"]!.GetValue<string>())!;
            var kind =
                report == "distribution-sheriffs"
                    ? "user"
                    : key["kind"]!.GetValue<string>() switch
                    {
                        "Scheduled" => "shift",
                        "Leave" => "leave",
                        "AwayLocation" => "loanOut",
                        "Training" => "training",
                        _ => throw new InvalidOperationException("Unreviewed source event vocabulary."),
                    };
            expected.Add(
                new(
                    row["Window"]!.GetValue<string>(),
                    report == "distribution-sheriffs" ? "resources" : "availability-events",
                    key["locationId"]!.ToString(),
                    key["sheriffId"]!.ToString(),
                    kind,
                    report == "distribution-sheriffs" ? null : DateTimeOffset.Parse(key["start"]!.GetValue<string>()),
                    report == "distribution-sheriffs" ? null : DateTimeOffset.Parse(key["end"]!.GetValue<string>())
                )
            );
        }
        // A declared common read projection only: no source formulas, aliases expanded,
        // missing target fields synthesized, or full distribution acceptance from this count.
        var summaries = expected
            .Select(x => x.Surface)
            .Distinct()
            .Select(surface =>
            {
                var a = expected
                    .Where(x => x.Surface == surface)
                    .GroupBy(x => x)
                    .ToDictionary(x => x.Key, x => x.Count());
                var b = observations
                    .Where(x => x.Surface == surface)
                    .GroupBy(x => x)
                    .ToDictionary(x => x.Key, x => x.Count());
                var exact = a.Sum(x => Math.Min(x.Value, b.GetValueOrDefault(x.Key)));
                return new
                {
                    Surface = surface,
                    Source = a.Values.Sum(),
                    Target = b.Values.Sum(),
                    CommonProjectionExact = exact,
                    Missing = a.Values.Sum() - exact,
                    Unexpected = b.Values.Sum() - exact,
                };
            })
            .ToArray();
        Step = "write-actual-read-evidence";
        await File.WriteAllTextAsync(
            output,
            JsonSerializer.Serialize(
                new
                {
                    Format = "unified-actual-report-surfaces/v1",
                    SourceDatabase = "ss_ready_v4",
                    TargetDatabase = db.Database.GetDbConnection().Database,
                    Calls = calls,
                    Observations = observations,
                    Summary = summaries,
                    FailedReads = failures,
                    FullDistributionParity = false,
                    UiCsvPdfExecuted = false,
                    UncomparedFields = new[]
                    {
                        "resource.start/end (target resource API has no span)",
                        "conflictLocationId",
                        "timezone",
                        "workSection",
                        "overtime",
                        "eventType",
                        "slotIds",
                    },
                    Unsupported = new[]
                    {
                        "Three training harness projection rows are not source or target UI execution.",
                        "ScheduleDistributionService delivers PDF/email; it has no SS availability DTO read contract.",
                    },
                }
            )
        );
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    Calls = calls.Count,
                    Summary = summaries,
                    FullDistributionParity = false,
                    UnexecutedHarnessProjectionRows = 3,
                    FailedReads = failures.Count,
                }
            )
        );
        return failures.Count == 0;
    }
}
