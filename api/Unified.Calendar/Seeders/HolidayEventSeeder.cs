using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Seeders;

public sealed class HolidayEventSeeder(
    ILogger<HolidayEventSeeder> logger,
    IOptions<CalendarSeedDataOptions> seedDataOptions
) : SeederBase<UnifiedDbContext>(logger)
{
    private readonly CalendarSeedDataOptions _seedDataOptions = seedDataOptions.Value;

    public override int Order => 100;

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting holiday event seeding.");

        var seedFilePath = ResolveSeedFilePath(_seedDataOptions.HolidaysFilePath);
        Logger.LogInformation("Using holiday seed data file at {SeedFilePath}.", seedFilePath);

        if (!File.Exists(seedFilePath))
        {
            Logger.LogWarning("Holiday seed file was not found at {SeedFilePath}.", seedFilePath);
            return;
        }

        await using var seedStream = File.OpenRead(seedFilePath);
        var seedDocument = await JsonSerializer.DeserializeAsync<HolidaySeedDocument>(
            seedStream,
            cancellationToken: cancellationToken
        );

        if (seedDocument?.Holidays is null || seedDocument.Holidays.Count == 0)
        {
            Logger.LogWarning("No holiday seed data was found in {SeedFilePath}.", seedFilePath);
            return;
        }

        var createdCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        for (var index = 0; index < seedDocument.Holidays.Count; index++)
        {
            var holiday = seedDocument.Holidays[index];

            if (string.IsNullOrWhiteSpace(holiday.Name) || string.IsNullOrWhiteSpace(holiday.ActualDate))
            {
                skippedCount++;
                Logger.LogWarning("Skipping holiday seed row {RowIndex} due to missing required values.", index);
                continue;
            }

            var startAtUtc = ParseHolidayStartAtUtc(holiday.ActualDate);
            var endAtUtc = startAtUtc.AddDays(1);

            var existingEvent = await dbContext.Events.FirstOrDefaultAsync(
                eventEntity =>
                    eventEntity.SourceModule == CalendarConstants.SourceModule
                    && eventEntity.EventTypeCode == CalendarEventTypeCodes.Holiday
                    && eventEntity.Title == holiday.Name
                    && eventEntity.StartAtUtc == startAtUtc,
                cancellationToken
            );

            if (existingEvent is null)
            {
                await dbContext.Events.AddAsync(
                    new Event
                    {
                        Title = holiday.Name,
                        StartAtUtc = startAtUtc,
                        EndAtUtc = endAtUtc,
                        AllDay = true,
                        EventTypeCode = CalendarEventTypeCodes.Holiday,
                        StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                        SourceModule = CalendarConstants.SourceModule,
                    },
                    cancellationToken
                );

                createdCount++;
                continue;
            }

            existingEvent.Title = holiday.Name;
            existingEvent.EndAtUtc = endAtUtc;
            existingEvent.TimeZoneId = null;
            existingEvent.AllDay = true;
            existingEvent.EventTypeCode = CalendarEventTypeCodes.Holiday;
            existingEvent.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
            existingEvent.SourceModule = CalendarConstants.SourceModule;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Holiday event seeding complete. Created {CreatedCount}, updated {UpdatedCount}, skipped {SkippedCount}.",
            createdCount,
            updatedCount,
            skippedCount
        );
    }

    private static string ResolveSeedFilePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private static DateTimeOffset ParseHolidayStartAtUtc(string actualDate)
    {
        var date = DateOnly.ParseExact(actualDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private sealed record HolidaySeedDocument(
        string? Source,
        string? SourceUrl,
        IReadOnlyList<HolidaySeedRow> Holidays
    );

    private sealed record HolidaySeedRow(string? Name, string? ActualDate);
}
