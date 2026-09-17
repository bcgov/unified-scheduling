using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeds Calendar EventType codes owned by Unified.UserManagement (e.g. "leave"), mirroring Unified.Calendar's EventTypeSeeder.
/// </summary>
public sealed class UserManagementCalendarEventTypeSeeder(ILogger<UserManagementCalendarEventTypeSeeder> logger)
    : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly EventType[] SeedEventTypes =
    [
        new()
        {
            Code = UserManagementConstants.LeaveEventTypeCode,
            Description = UserManagementConstants.LeaveEventTypeDescription,
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    public override int Order => 13;

    public override string Name => "UserManagementCalendarEventTypes";

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
