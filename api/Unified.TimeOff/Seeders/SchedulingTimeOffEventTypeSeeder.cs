using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.TimeOff.Constants;

namespace Unified.TimeOff.Seeders;

public sealed class SchedulingTimeOffEventTypeSeeder(ILogger<SchedulingTimeOffEventTypeSeeder> logger)
    : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    public override int Order => 11;

    public override string Name => "SchedulingTimeOffEventType";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        // Upsert the TimeOff event type.
        await UpsertEventTypeAsync(
            dbContext,
            TimeOffConstants.TimeOffEventTypeCode,
            TimeOffConstants.TimeOffEventTypeDescription,
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
