using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Reporting;
using Unified.Common.Time;
using Unified.Db;
using Unified.Leave.FeatureFlags;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;
using Unified.Training.Services.Reporting;

internal static class BusinessQueryExport
{
    // Calls actual target query implementations. No copied compliance/overtime formula.
    // Deliberately excludes all display names, descriptions and notes from evidence.
    public static async Task ExecuteAsync(UnifiedDbContext db, IConfiguration config)
    {
        var defaultZone = config["CalendarDateTime:DefaultTimeZoneId"];
        if (string.IsNullOrWhiteSpace(defaultZone))
            throw new InvalidOperationException("Explicit calendar timezone required.");
        if (config.GetValue<bool>("FeatureFlags:LoanOut:Enabled"))
            throw new InvalidOperationException("Loan-out enabled export requires the full scheduling query host.");
        var timeService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = defaultZone }),
            timeService
        );
        var workingOptions = config.GetSection("WorkingHours").Get<WorkingHoursOptions>() ?? new WorkingHoursOptions();
        if (workingOptions.FullWorkingDayMinutes != 420)
            throw new InvalidOperationException("Expected 420-minute working day.");
        var availability = new ShiftLoanOutAvailabilityService(
            db,
            resolver,
            timeService,
            () => throw new InvalidOperationException("Disabled loan-out path must not invoke shift writes."),
            false
        );
        var workingHours = new WorkingHoursService(
            NullLogger<WorkingHoursService>.Instance,
            db,
            Options.Create(workingOptions),
            timeService,
            Options.Create(new LeaveFeatureFlags { Enabled = config.GetValue<bool>("FeatureFlags:Leave:Enabled") }),
            resolver,
            availability
        );
        var users = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == "User")
            .ToDictionaryAsync(x => Guid.Parse(x.TargetKey), x => x.LegacyId);
        var courses = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == "Training")
            .ToDictionaryAsync(x => int.Parse(x.TargetKey), x => x.LegacyId);
        var report = new UserTrainingReportQueryHandler(db, TimeProvider.System);
        foreach (
            var (name, start, end) in new[]
            {
                ("past-month", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)),
                ("cutover-14", new DateOnly(2026, 11, 17), new DateOnly(2026, 12, 15)),
                ("first-future-month", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)),
            }
        )
        {
            foreach (var user in users)
            {
                var response = (UserTrainingReportResponse)
                    await report.ExecuteAsync(
                        new Dictionary<string, IReadOnlyCollection<string>>
                        {
                            ["userId"] = [user.Key.ToString()],
                            ["startDate"] = [start.ToString("yyyy-MM-dd")],
                            ["endDate"] = [end.ToString("yyyy-MM-dd")],
                        },
                        null,
                        SortDirection.Asc
                    );
                foreach (var row in response.Rows)
                    Console.WriteLine(
                        JsonSerializer.Serialize(
                            new
                            {
                                Query = "UserTrainingReportQueryHandler",
                                Window = name,
                                SourceUserId = user.Value,
                                SourceCourseId = courses.GetValueOrDefault(row.TrainingId),
                                row.AwardedOn,
                                row.EndingOn,
                                row.ExpiryDate,
                                row.Status,
                                row.Version,
                                row.NoticeState,
                                row.HasMissingMandatoryTrainingAssignment,
                            }
                        )
                    );
            }
            foreach (
                var row in await workingHours.QueryAsync(new WorkingHoursQuery { StartDate = start, EndDate = end })
            )
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new
                        {
                            Query = "WorkingHoursService",
                            Window = name,
                            SourceUserId = users.GetValueOrDefault(row.UserId),
                            row.ShiftEntryId,
                            row.Date,
                            row.PaidShiftMinutes,
                            row.CreditedMinutes,
                            row.OvertimeMinutes,
                        }
                    )
                );
        }
    }
}
