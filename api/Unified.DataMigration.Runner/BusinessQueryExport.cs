using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Reporting;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.LoanOut;
using Unified.Leave.FeatureFlags;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Services;
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
            () => throw new InvalidOperationException("Read-only export must not invoke shift writes."),
            config.GetValue<bool>("FeatureFlags:LoanOut:Enabled")
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
        var loanOut = new LoanOutCalendarDataProvider(db, new LoanOutCalendarMapper(), timeService);
        if (availability.IsEnabled)
        {
            var awayLinks = await db
                .DataMigrationRecords.AsNoTracking()
                .Where(x => x.Source == "SS" && x.EntityType == "SheriffAwayLocation")
                .ToListAsync();
            foreach (var link in awayLinks)
            {
                var entry = await db
                    .LoanOutEntries.AsNoTracking()
                    .Include(x => x.Event)
                    .Include(x => x.Users)
                    .SingleAsync(x => x.Id == int.Parse(link.TargetKey));
                if (entry.Event!.StatusTypeCode == "cancelled")
                    continue;
                var ids = entry.Users.Select(x => x.UserId).ToList();
                var occurrences = new[]
                {
                    new ShiftAssignmentLoanOutContext(entry.Event.StartAtUtc, entry.Event.EndAtUtc),
                };
                var atDestination = await availability.GetAssignableUserIdsByOccurrenceAsync(
                    ids,
                    occurrences,
                    entry.LoanOutLocationId,
                    entry.Event.TimeZoneId
                );
                var atHome = await availability.GetAssignableUserIdsByOccurrenceAsync(
                    ids,
                    occurrences,
                    entry.Event.LocationId,
                    entry.Event.TimeZoneId
                );
                if (
                    atDestination.Single().Count != ids.Count
                    || (entry.Event.LocationId != entry.LoanOutLocationId && atHome.Single().Count != 0)
                )
                    throw new InvalidOperationException(
                        "Actual LoanOut availability did not consume the migrated away graph."
                    );
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new
                        {
                            Query = "ShiftLoanOutAvailabilityService",
                            SourceAwayId = link.LegacyId,
                            DestinationAssignable = atDestination.Single().Count,
                            HomeAssignable = atHome.Single().Count,
                        }
                    )
                );
            }
        }
        foreach (var (name, start, end) in ActualRehearsalValidation.Windows)
        {
            if (availability.IsEnabled)
            {
                var range = timeService.ConvertInclusiveLocalDateRangeToUtcRange(
                    start,
                    end,
                    timeService.ResolveOrUtc(defaultZone)
                );
                var context = new SchedulingCalendarEventQueryContext
                {
                    StartAtUtc = range.StartAtUtc,
                    EndAtUtc = range.EndAtUtc,
                };
                var awayEvents = await loanOut.GetDataAsync(context);
                foreach (var item in awayEvents)
                    Console.WriteLine(
                        JsonSerializer.Serialize(
                            new
                            {
                                Query = "LoanOutCalendarDataProvider",
                                Window = name,
                                item.Id,
                                item.Start,
                                item.End,
                                item.UserIds,
                            }
                        )
                    );
            }
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
