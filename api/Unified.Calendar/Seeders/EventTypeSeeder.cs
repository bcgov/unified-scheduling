using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Calendar.Seeders;

public sealed class EventTypeSeeder(ILogger<EventTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly EventType[] SeedEventTypes =
    [
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.General),
            Description = "General",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Holiday),
            Description = "Holiday",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.Deadline),
            Description = "Deadline",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventTypeCode.AwayLocation),
            Description = "Away Location",
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    public override int Order => 10;

    public override string Name => "CalendarEventTypes";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var seedEventType in SeedEventTypes)
        {
            var existingEventType = await dbContext.EventTypes.FirstOrDefaultAsync(
                eventType => eventType.Code == seedEventType.Code,
                cancellationToken
            );

            if (existingEventType is null)
            {
                await dbContext.EventTypes.AddAsync(seedEventType, cancellationToken);
                continue;
            }

            existingEventType.Description = seedEventType.Description;
            existingEventType.EffectiveDate = seedEventType.EffectiveDate;
            existingEventType.ExpiryDate = seedEventType.ExpiryDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
