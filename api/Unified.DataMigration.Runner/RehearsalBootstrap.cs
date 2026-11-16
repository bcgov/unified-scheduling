using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Seeders;
using Unified.Db;
using Unified.Leave.Seeders;
using Unified.LoanOut.Seeders;
using Unified.Scheduling.Seeders;
using Unified.Stats.Seeders;
using Unified.Training;
using Unified.Training.Seeders;
using Unified.UserManagement;
using Unified.UserManagement.Seeders;

internal static class RehearsalBootstrap
{
    public static async Task AddMissingLoanOutReferenceAsync(UnifiedDbContext db)
    {
        // Upgrade only the original empty business baseline. Never rerun bootstrap
        // against a populated target or touch an application/shared database.
        if (
            db.Database.GetDbConnection().Database != "unified_ss_rehearsal"
            || await db.Users.CountAsync() != 1
            || await db.DataMigrationRecords.AnyAsync()
            || await db.Events.AnyAsync()
        )
            throw new InvalidOperationException("Reference upgrade requires the isolated empty business baseline.");
        await new LoanOutEventTypeSeeder(NullLogger<LoanOutEventTypeSeeder>.Instance).SeedAsync(db);
    }

    // Explicit, local-only bootstrap, NOT called by migration execution or app startup.
    // Runs real reference seeders so the rehearsal does not substitute invented target defaults.
    public static async Task ExecuteAsync(UnifiedDbContext db)
    {
        if (
            db.Database.GetDbConnection().Database
                is not (
                    "unified_ss_rehearsal"
                    or "unified_ss_rehearsal_ready"
                    or "unified_ss_rehearsal_ready_v2"
                    or "unified_ss_rehearsal_ready_v3"
                    or "unified_ss_rehearsal_ready_v4"
                    or "unified_ss_rehearsal_ready_v4_recovery"
                    or "unified_ss_rehearsal_ready_v4_probe"
                )
            || await db.Users.AnyAsync()
            || await db.DataMigrationRecords.AnyAsync()
        )
            throw new InvalidOperationException("Bootstrap requires the empty isolated rehearsal target.");
        await new UserSeeder(
            NullLogger<UserSeeder>.Instance,
            [
                new UserSeedConfiguration
                {
                    Source = "PlatformSystemUserDataSet",
                    Definitions = PlatformSystemUserSeedData.Instance.Definitions,
                },
            ]
        ).SeedAsync(db);
        await new RoleSeeder(
            NullLogger<RoleSeeder>.Instance,
            [
                new RoleSeedConfiguration
                {
                    Source = "DefaultRolesDataSet",
                    Definitions = DefaultRoleSeedData.Instance.Definitions,
                },
            ]
        ).SeedAsync(db);
        await new LeaveTypeSeeder(NullLogger<LeaveTypeSeeder>.Instance).SeedAsync(db);
        await new EventStatusTypeSeeder(NullLogger<EventStatusTypeSeeder>.Instance).SeedAsync(db);
        await new ShiftEventTypeSeeder(NullLogger<ShiftEventTypeSeeder>.Instance).SeedAsync(db);
        await new SchedulingLeaveEventTypeSeeder(NullLogger<SchedulingLeaveEventTypeSeeder>.Instance).SeedAsync(db);
        await new LoanOutEventTypeSeeder(NullLogger<LoanOutEventTypeSeeder>.Instance).SeedAsync(db);
        await new TrainingProfileTypeSeeder(
            NullLogger<TrainingProfileTypeSeeder>.Instance,
            [
                new TrainingProfileTypeSeedConfiguration
                {
                    Source = "TrainingProfileTypes",
                    Definitions = TrainingProfileTypeSeedData.Instance.Definitions,
                },
            ]
        ).SeedAsync(db);
        await new StatGroupSeeder(NullLogger<StatGroupSeeder>.Instance).SeedAsync(db);
        await new StatCategorySeeder(NullLogger<StatCategorySeeder>.Instance).SeedAsync(db);
        await new SubCategorySeeder(NullLogger<SubCategorySeeder>.Instance).SeedAsync(db);
    }
}
