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
        await UpsertEventTypeAsync(
            dbContext,
            SchedulingConstants.ShiftEventTypeCode,
            SchedulingConstants.ShiftEventTypeDescription,
            cancellationToken
        );
        await UpsertEventTypeAsync(
            dbContext,
            SchedulingConstants.AssignmentEventTypeCode,
            SchedulingConstants.AssignmentEventTypeDescription,
            cancellationToken
        );
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertEventTypeAsync(
        UnifiedDbContext dbContext,
        string code,
        string description,
        CancellationToken cancellationToken
    )
    {
        var existingEventType = await dbContext.EventTypes.FirstOrDefaultAsync(
            eventType => eventType.Code == code,
            cancellationToken
        );

        if (existingEventType is null)
        {
            await dbContext.EventTypes.AddAsync(
                new EventType
                {
                    Code = code,
                    Description = description,
                    EffectiveDate = SeedEffectiveDate,
                },
                cancellationToken
            );
            return;
        }

        existingEventType.Description = description;
        existingEventType.EffectiveDate = SeedEffectiveDate;
        existingEventType.ExpiryDate = null;
    }
}
