using Cronos;
using Microsoft.Extensions.Options;

namespace Unified.DataMigration.Options;

public sealed class DataMigrationOptions
{
    public const string SectionName = "DataMigration";
    public const int DefaultCommandTimeoutSeconds = 28800;

    public DataMigrationSourcesOptions Sources { get; set; } = new();

    // Migration batches can legitimately run longer than the Npgsql/EF defaults. This
    // timeout is applied only to migration source/target work, never to the global DbContext.
    public int CommandTimeoutSeconds { get; set; } = DefaultCommandTimeoutSeconds;
}

public sealed class DataMigrationSourcesOptions
{
    public DataMigrationSourceOptions SS { get; set; } = new();
    public DataMigrationSourceOptions CASS { get; set; } = new();
}

public sealed class DataMigrationSourceOptions
{
    public bool Enabled { get; set; }
    public string? CronSchedule { get; set; }
    public string? ConnectionString { get; set; }
    public int BatchSize { get; set; } = 100;
    public bool DryRun { get; set; } = true;
    public DateOnly? CutoverDate { get; set; }
    public DateOnly HistoryStartDate { get; set; } = new(2020, 1, 1);
    public SsJobOptions Jobs { get; set; } = new();
    public bool AllowUnrecoverableSyntheticApply { get; set; }
}

public sealed class SsJobOptions
{
    public MigrationJobOptions MyTeam { get; set; } = new();
    public MigrationJobOptions Training { get; set; } = new();
    public MigrationJobOptions Scheduling { get; set; } = new();
    public MigrationJobOptions Report { get; set; } = new();

    public MigrationJobOptions For(string job) =>
        job switch
        {
            "MyTeam" => MyTeam,
            "Training" => Training,
            "Scheduling" => Scheduling,
            "Report" => Report,
            _ => throw new ArgumentOutOfRangeException(nameof(job)),
        };
}

public sealed class MigrationJobOptions
{
    public bool Enabled { get; set; }
    public string? CronSchedule { get; set; }
    public bool DryRun { get; set; } = true;
    public bool AllowMigrationApply { get; set; }
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// <see cref="MigrationPersistenceMode.PerRow"/> (default) keeps the per-row writer and rollback journal.
    /// <see cref="MigrationPersistenceMode.Bulk"/> (Training and Scheduling) persists set-based per chunk without a journal.
    /// </summary>
    public MigrationPersistenceMode PersistenceMode { get; set; } = MigrationPersistenceMode.PerRow;

    /// <summary>
    /// Approximate source rows (Training) or writer units (Scheduling) per Bulk chunk transaction. A user's
    /// Training histories and a Scheduling unit (merge group, duty graph, series, captured component) are never split.
    /// </summary>
    public int BulkChunkSize { get; set; } = 2000;

    /// <summary>
    /// Scheduling PerRow: skip the full graph proposal plan when no captured component needs graph replacement.
    /// Proposals are only read during replacement, so the outcome is identical; off keeps the always-plan path.
    /// </summary>
    public bool PlanReplacementFamiliesOnly { get; set; }
}

public enum MigrationPersistenceMode
{
    PerRow,
    Bulk,
}

public sealed class DataMigrationOptionsValidator : IValidateOptions<DataMigrationOptions>
{
    public ValidateOptionsResult Validate(string? name, DataMigrationOptions options)
    {
        var failures = new List<string>();
        if (options.CommandTimeoutSeconds <= 0)
            failures.Add("DataMigration:CommandTimeoutSeconds must be greater than zero.");
        ValidateSource("SS", options.Sources.SS, failures);
        ValidateSource("CASS", options.Sources.CASS, failures);
        if (options.Sources.SS.Enabled)
        {
            if (options.Sources.SS.CutoverDate != new DateOnly(2026, 12, 1))
                failures.Add("DataMigration:Sources:SS:CutoverDate must explicitly be 2026-12-01.");
            foreach (var job in new[] { "MyTeam", "Training", "Scheduling", "Report" })
            {
                var settings = options.Sources.SS.Jobs.For(job);
                // Persistence mode is checked even for disabled jobs so an unsupported Bulk setting
                // cannot sit unnoticed and be recorded in trigger evidence later.
                if (!Enum.IsDefined(settings.PersistenceMode))
                    failures.Add($"SS:{job}:PersistenceMode is invalid.");
                if (
                    settings.PersistenceMode == MigrationPersistenceMode.Bulk
                    && job is not ("Training" or "Scheduling")
                )
                    failures.Add($"SS:{job}:PersistenceMode Bulk is supported only for Training and Scheduling.");
                if (!settings.Enabled)
                    continue;
                if (settings.BatchSize <= 0)
                    failures.Add($"SS:{job}:BatchSize must be positive.");
                if (settings.BulkChunkSize <= 0)
                    failures.Add($"SS:{job}:BulkChunkSize must be positive.");
                if (
                    !string.IsNullOrWhiteSpace(settings.CronSchedule)
                    && !settings.CronSchedule.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                    && !settings.CronSchedule.Equals("disable", StringComparison.OrdinalIgnoreCase)
                )
                {
                    try
                    {
                        CronExpression.Parse(settings.CronSchedule);
                    }
                    catch (CronFormatException)
                    {
                        failures.Add($"SS:{job}:CronSchedule is invalid.");
                    }
                }
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateSource(string source, DataMigrationSourceOptions options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"DataMigration:Sources:{source}:ConnectionString is required when enabled.");
        }

        if (
            !string.IsNullOrWhiteSpace(options.CronSchedule)
            && !options.CronSchedule.Equals("disable", StringComparison.OrdinalIgnoreCase)
            && !options.CronSchedule.Equals("disabled", StringComparison.OrdinalIgnoreCase)
        )
        {
            try
            {
                CronExpression.Parse(options.CronSchedule);
            }
            catch (CronFormatException)
            {
                failures.Add(
                    $"DataMigration:Sources:{source}:CronSchedule must be a valid five-field cron expression."
                );
            }
        }

        if (options.BatchSize <= 0)
        {
            failures.Add($"DataMigration:Sources:{source}:BatchSize must be greater than zero when enabled.");
        }
    }
}
