using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Calendar.Seeders;

public sealed class EventStatusTypeSeeder(ILogger<EventStatusTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly EventStatusType[] SeedEventStatusTypes =
    [
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Draft),
            Description = "Draft",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Active),
            Description = "Active",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = CalendarCodeMappings.ToDbCode(CalendarEventStatusTypeCode.Cancelled),
            Description = "Cancelled",
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    public override int Order => 20;

    public override string Name => "CalendarEventStatusTypes";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var seedEventStatusType in SeedEventStatusTypes)
        {
            var existingEventStatusType = await dbContext.EventStatusTypes.FirstOrDefaultAsync(
                eventStatusType => eventStatusType.Code == seedEventStatusType.Code,
                cancellationToken
            );

            if (existingEventStatusType is null)
            {
                await dbContext.EventStatusTypes.AddAsync(seedEventStatusType, cancellationToken);
                continue;
            }

            existingEventStatusType.Description = seedEventStatusType.Description;
            existingEventStatusType.EffectiveDate = seedEventStatusType.EffectiveDate;
            existingEventStatusType.ExpiryDate = seedEventStatusType.ExpiryDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
