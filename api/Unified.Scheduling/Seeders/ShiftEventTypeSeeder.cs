using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Scheduling.Seeders;

public sealed class ShiftEventTypeSeeder(ILogger<ShiftEventTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    public override int Order => 11;

    public override string Name => "SchedulingShiftEventType";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingEventType = await dbContext.EventTypes.FirstOrDefaultAsync(
            eventType => eventType.Code == SchedulingConstants.ShiftEventTypeCode,
            cancellationToken
        );

        if (existingEventType is null)
        {
            await dbContext.EventTypes.AddAsync(
                new EventType
                {
                    Code = SchedulingConstants.ShiftEventTypeCode,
                    Description = SchedulingConstants.ShiftEventTypeDescription,
                    EffectiveDate = SeedEffectiveDate,
                },
                cancellationToken
            );
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        existingEventType.Description = SchedulingConstants.ShiftEventTypeDescription;
        existingEventType.EffectiveDate = SeedEffectiveDate;
        existingEventType.ExpiryDate = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
