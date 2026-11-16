using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Audit;
using Unified.Audit.Options;
using Unified.Calendar;
using Unified.Common.Scheduling;
using Unified.DataMigration;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Leave;
using Unified.LoanOut;
using Unified.LoanOut.Services;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

// Migration-only CLI. It does not host the application, run its seeders or fire domain signals.
// Connection strings must come from environment/configuration, never command-line arguments.
var environment = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var builder = new ConfigurationBuilder();
if (environment["DataMigrationRehearsal:BusinessConfigurationPath"] is { } businessConfigurationPath)
    builder.AddJsonFile(businessConfigurationPath, optional: false);
var configuration = builder.AddEnvironmentVariables().Build();
var connection = configuration["DatabaseConnectionString"];
if (string.IsNullOrWhiteSpace(connection) || args.Length != 1)
{
    Console.Error.WriteLine(
        "Set configuration; use schema|bootstrap|seed-loanout-reference|MyTeam|Training|Scheduling|Report|evidence|queries|publish-preview|publish-rehearsal|export-oracle|recover."
    );
    return 2;
}
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddDbContext<UnifiedDbContext>(o => o.UseNpgsql(connection));
services.AddLogging();
services.AddAuditModule(configuration);

// This is a dedicated migration process, not an application-wide audit policy change.
// Keep Unified audit identities/actions, but do not replicate private source values
// into audit logs. The business targets alone retain the transferred values.
services.PostConfigure<AuditRecordOptions>(audit =>
    audit.ExcludedPropertyNames = [
        .. audit.ExcludedPropertyNames,
        "FirstName",
        "LastName",
        "Email",
        "IdirName",
        "IdirId",
        "KeyCloakId",
        "Gender",
        "LastLogin",
        "Rank",
        "BadgeNumber",
        "EmployeeNumber",
        "Name",
        "Description",
        "Title",
        "Notes",
        "Note",
        "Comment",
        "ExpiryReason",
        "CancellationReason",
        "CancelledReason",
        "Photo",
    ]
);
services.AddDataMigrationModule(configuration);
services.AddCalendarModule(configuration);
services.AddSchedulingModule(configuration);
services.AddLeaveModule(configuration);
services.AddLoanOutModule(configuration);
if (LoanOutModule.IsModuleEnabled(configuration))
    services.AddScoped<ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>>(sp =>
        sp.GetRequiredService<ILoanOutCalendarDataProvider>()
    );
services.AddScoped<ISsMigrationBusinessValidator, ActualRehearsalValidation>();
await using var provider = services.BuildServiceProvider();
provider.UseAuditModule();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<UnifiedDbContext>();
try
{
    if (args[0] == "schema")
    {
        // Explicit command only. Never automatically migrate or reset a shared database.
        await db.Database.MigrateAsync();
        Console.WriteLine("Schema applied.");
    }
    else if (args[0] == "bootstrap")
    {
        await RehearsalBootstrap.ExecuteAsync(db);
        Console.WriteLine("Isolated rehearsal references seeded using application seeders.");
    }
    else if (args[0] == "queries")
    {
        await BusinessQueryExport.ExecuteAsync(db, configuration);
    }
    else if (args[0] == "seed-loanout-reference")
    {
        await RehearsalBootstrap.AddMissingLoanOutReferenceAsync(db);
        Console.WriteLine("Isolated empty baseline LoanOut reference seeded.");
    }
    else if (args[0] is "publish-preview" or "publish-rehearsal")
    {
        await ActualRehearsalValidation.PublishAsync(
            scope.ServiceProvider,
            configuration,
            args[0] == "publish-rehearsal"
        );
    }
    else if (args[0] == "export-oracle")
    {
        return await NormalizedTargetExport.ExecuteAsync(scope.ServiceProvider, configuration) ? 0 : 1;
    }
    else if (args[0] == "recover")
    {
        var runId = Guid.Parse(configuration["DataMigrationRecovery:RunId"]!);
        var staleBefore = DateTimeOffset
            .Parse(configuration["DataMigrationRecovery:StaleBeforeUtc"]!)
            .ToUniversalTime();
        var recovered = await scope
            .ServiceProvider.GetRequiredService<DataMigrationControlService>()
            .RecoverStaleRunAsync(runId, staleBefore, CancellationToken.None);
        Console.WriteLine(recovered ? "Stale SS run fenced and abandoned." : "SS run was not eligible for recovery.");
        return recovered ? 0 : 1;
    }
    else if (args[0] == "evidence")
    {
        // Fixed columns: no source/target personal text or raw provider diagnostics.
        Console.WriteLine("RunId,Job,DryRun,Status,Scanned,Rejected,Failed");
        foreach (
            var run in await db
                .DataMigrationRuns.AsNoTracking()
                .Where(x => x.Source == "SS")
                .OrderBy(x => x.StartedOn)
                .ToListAsync()
        )
            Console.WriteLine(
                $"{run.Id},{run.Job},{run.DryRun},{run.Status},{run.ScannedCount},{run.RejectedCount},{run.FailedCount}"
            );
        Console.WriteLine("RunId,EntityType,Outcome,Reason,Count");
        foreach (
            var metric in await db
                .DataMigrationOutcomes.AsNoTracking()
                .GroupBy(x => new
                {
                    x.RunId,
                    x.EntityType,
                    x.Outcome,
                    x.Reason,
                })
                .Select(x => new
                {
                    x.Key.RunId,
                    x.Key.EntityType,
                    x.Key.Outcome,
                    x.Key.Reason,
                    Count = x.Count(),
                })
                .ToListAsync()
        )
            Console.WriteLine($"{metric.RunId},{metric.EntityType},{metric.Outcome},{metric.Reason},{metric.Count}");
    }
    else
    {
        await scope
            .ServiceProvider.GetRequiredService<SsMigrationExecutor>()
            .ExecuteAsync(args[0], CancellationToken.None);
        var latest = await db
            .DataMigrationRuns.AsNoTracking()
            .Where(x => x.Source == "SS" && x.Job == args[0])
            .OrderByDescending(x => x.StartedOn)
            .FirstAsync();
        Console.WriteLine(
            $"Run={latest.Id}; Job={latest.Job}; Status={latest.Status}; Scanned={latest.ScannedCount}; Rejected={latest.RejectedCount}; Failed={latest.FailedCount}"
        );
        return latest.Status == "Completed" ? 0 : 1;
    }
    return 0;
}
catch (Exception error)
{
    // Never print exception.ToString(): source/database diagnostics may contain private values.
    Console.Error.WriteLine("Migration command failed. Inspect migration-only outcomes and configuration.");
    Console.Error.WriteLine($"FailureType={error.GetType().Name}");
    if (error is Microsoft.Extensions.Options.OptionsValidationException optionError)
        Console.Error.WriteLine($"InvalidOptionsType={optionError.OptionsType.FullName}");
    return 1;
}
