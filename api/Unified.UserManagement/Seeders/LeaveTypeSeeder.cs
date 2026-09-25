using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the LeaveType table.
/// </summary>
public sealed class LeaveTypeSeeder(ILogger<LeaveTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override int Order => 12;

    public override string Name => "LeaveType";

    // Codes and ids match the legacy LookupCode LeaveType entries in sheriff-scheduling/court-admin-scheduling.
    private static readonly LeaveType[] SeedLeaveTypes =
    [
        new()
        {
            Id = 17,
            Code = "STIP",
            Description = "STIP",
            IsPaid = false,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Id = 18,
            Code = "Annual",
            Description = "Annual",
            IsPaid = false,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Id = 19,
            Code = "Illness",
            Description = "Illness",
            IsPaid = false,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Id = 20,
            Code = "Special",
            Description = "Special",
            IsPaid = false,
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var seedLeaveType in SeedLeaveTypes)
        {
            var existingLeaveType = await dbContext.LeaveTypes.FirstOrDefaultAsync(
                leaveType => leaveType.Code == seedLeaveType.Code,
                cancellationToken
            );

            if (existingLeaveType is null)
            {
                await dbContext.LeaveTypes.AddAsync(seedLeaveType, cancellationToken);
                continue;
            }

            existingLeaveType.Description = seedLeaveType.Description;
            existingLeaveType.IsPaid = seedLeaveType.IsPaid;
            existingLeaveType.EffectiveDate = seedLeaveType.EffectiveDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
