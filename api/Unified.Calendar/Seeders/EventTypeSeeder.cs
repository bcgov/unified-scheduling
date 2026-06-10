using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Calendar.Seeders;

public sealed class EventTypeSeeder(ILogger<EventTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly EventType[] SeedEventTypes =
    [
        new() { Code = CalendarEventTypeCodes.General, Description = "General", EffectiveDate = SeedEffectiveDate },
        new() { Code = CalendarEventTypeCodes.Holiday, Description = "Holiday", EffectiveDate = SeedEffectiveDate },
        new() { Code = CalendarEventTypeCodes.Deadline, Description = "Deadline", EffectiveDate = SeedEffectiveDate },
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