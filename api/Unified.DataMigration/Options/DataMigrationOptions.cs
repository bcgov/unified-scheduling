using Cronos;
using Microsoft.Extensions.Options;

namespace Unified.DataMigration.Options;

public sealed class DataMigrationOptions
{
    public const string SectionName = "DataMigration";

    public DataMigrationSourcesOptions Sources { get; set; } = new();
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
    public DateOnly HistoryStartDate { get; set; } = new(2015, 1, 1);
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
    public int BatchSize { get; set; } = 100;
}

public sealed class DataMigrationOptionsValidator : IValidateOptions<DataMigrationOptions>
{
    public ValidateOptionsResult Validate(string? name, DataMigrationOptions options)
    {
        var failures = new List<string>();
        ValidateSource("SS", options.Sources.SS, failures);
        ValidateSource("CASS", options.Sources.CASS, failures);
        if (options.Sources.SS.Enabled)
        {
            if (options.Sources.SS.CutoverDate != new DateOnly(2026, 12, 1))
                failures.Add("DataMigration:Sources:SS:CutoverDate must explicitly be 2026-12-01.");
            foreach (var job in new[] { "MyTeam", "Training", "Scheduling", "Report" })
            {
                var settings = options.Sources.SS.Jobs.For(job);
                if (!settings.Enabled)
                    continue;
                if (settings.BatchSize <= 0)
                    failures.Add($"SS:{job}:BatchSize must be positive.");
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
