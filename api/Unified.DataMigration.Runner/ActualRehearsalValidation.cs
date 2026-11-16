using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

internal sealed class ActualRehearsalValidation(
    UnifiedDbContext db,
    IConfiguration config,
    ISchedulingCalendarService calendar,
    ISchedulePublishService publication,
    IServiceProvider services
) : ISsMigrationBusinessValidator
{
    internal static readonly (string Name, DateOnly Start, DateOnly End)[] Windows =
    [
        ("pastmonth", new(2026, 11, 1), new(2026, 11, 30)),
        ("cutover-minus14-plus14", new(2026, 11, 17), new(2026, 12, 14)),
        ("futuremonth", new(2027, 1, 1), new(2027, 1, 31)),
    ];

    public async Task ValidateAsync(
        string job,
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        CancellationToken ct
    )
    {
        await BusinessQueryExport.ExecuteAsync(db, config);
        var locations = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == "Location")
            .Select(x => x.TargetKey)
            .ToListAsync(ct);
        foreach (var location in locations.Select(int.Parse))
        foreach (var window in Windows)
        {
            var result = await calendar.GetDataAsync(
                new SchedulingCalendarRequest
                {
                    StartDate = window.Start,
                    EndDate = window.End,
                    LocationId = location,
                },
                true,
                true,
                true,
                config.GetValue<bool>("FeatureFlags:LoanOut:Enabled"),
                ct
            );
            var preview = await publication.PreviewAsync(
                new SchedulePublishRequest(location, window.Start, window.End),
                ct
            );
            var blocked = preview.Candidates.TotalCount > 0 && !preview.CanPublish;
            var key = $"{session.Run.Id}:{location}:{window.Name}";
            session.Outcome(
                "SchedulingReadValidation",
                key,
                blocked ? "Failed" : "Migrated",
                blocked ? "ActualPublishPreviewBlocked" : "ActualCalendarReadAndPublishPreviewExecuted"
            );
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new
                    {
                        Query = "SchedulingCalendarService+SchedulePublishService",
                        Window = window.Name,
                        LocationId = location,
                        EventCount = result.Events.Count,
                        preview.Candidates,
                        preview.CanPublish,
                        ConflictCount = preview.Blockers.Conflicts.Count + preview.Blockers.ShiftConflicts.Count,
                    }
                )
            );
        }
        await AwayAvailabilityImpact.ValidateAsync(services, session, ct);
        if (job == "Report")
        {
            var matches = await NormalizedTargetExport.ExecuteAsync(
                services,
                config,
                $"target-report-{session.Run.Id}.json"
            );
            session.Outcome(
                "SourceOracleValidation",
                session.Run.Id.ToString(),
                matches ? "Migrated" : "Failed",
                matches
                    ? "ActualIndependentOracleRowsMatched"
                    : "ActualIndependentOracleDifferencesOrUnsupportedSurfaces"
            );
        }
    }

    internal static async Task PublishAsync(IServiceProvider services, IConfiguration config, bool apply)
    {
        var db = services.GetRequiredService<UnifiedDbContext>();
        var database = db.Database.GetDbConnection().Database;
        if (
            apply
            && (
                database != "unified_ss_rehearsal_ready_v3_publish"
                || config["DataMigrationRehearsal:ConfirmPublishDatabase"] != database
            )
        )
            throw new InvalidOperationException(
                "Publication writes require the separately cloned, explicitly acknowledged synthetic publish database."
            );
        var publisher = services.GetRequiredService<ISchedulePublishService>();
        foreach (
            var id in await db
                .DataMigrationRecords.AsNoTracking()
                .Where(x => x.Source == "SS" && x.EntityType == "Location")
                .Select(x => x.TargetKey)
                .ToListAsync()
        )
        {
            var request = new SchedulePublishRequest(int.Parse(id), new(2026, 12, 1), new(2026, 12, 31));
            var preview = await publisher.PreviewAsync(request);
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new
                    {
                        Query = "SchedulePublishService.Preview",
                        LocationId = id,
                        preview.CanPublish,
                        preview.Candidates,
                        ConflictCount = preview.Blockers.Conflicts.Count + preview.Blockers.ShiftConflicts.Count,
                    }
                )
            );
            if (!apply || preview.Candidates.TotalCount == 0)
                continue;
            if (!preview.CanPublish)
                throw new InvalidOperationException("Actual publication preview blocks this rehearsal.");
            var result = await publisher.PublishAsync(request, null);
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new
                    {
                        Query = "SchedulePublishService.Publish",
                        LocationId = id,
                        result.PublishedShiftEntryCount,
                        result.PublishedAssignmentEntryCount,
                    }
                )
            );
        }
    }
}
