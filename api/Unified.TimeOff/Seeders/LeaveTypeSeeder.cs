using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.TimeOff.Seeders;

public sealed class LeaveTypeSeeder(ILogger<LeaveTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    public override int Order => 10;

    public override string Name => "LeaveTypes";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var (code, details) in LeaveTypeConstants.LeaveTypes)
        {
            var existingLeaveType = await dbContext.LeaveTypes.FirstOrDefaultAsync(
                leaveType => leaveType.Code == code,
                cancellationToken
            );

            if (existingLeaveType is null)
            {
                dbContext.LeaveTypes.Add(
                    new LeaveType
                    {
                        Code = code,
                        Description = details.Description,
                        IsPaid = details.IsPaid,
                        EffectiveDate = SeedEffectiveDate,
                    }
                );

                continue;
            }

            existingLeaveType.Description = details.Description;
            existingLeaveType.IsPaid = details.IsPaid;
            existingLeaveType.EffectiveDate = SeedEffectiveDate;
            existingLeaveType.ExpiryDate = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
