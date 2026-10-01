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
    public bool DryRun { get; set; }
}

public sealed class DataMigrationOptionsValidator : IValidateOptions<DataMigrationOptions>
{
    public ValidateOptionsResult Validate(string? name, DataMigrationOptions options)
    {
        var failures = new List<string>();
        ValidateSource("SS", options.Sources.SS, failures);
        ValidateSource("CASS", options.Sources.CASS, failures);

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

        if (string.IsNullOrWhiteSpace(options.CronSchedule))
        {
            failures.Add($"DataMigration:Sources:{source}:CronSchedule is required when enabled.");
        }

        if (options.BatchSize <= 0)
        {
            failures.Add($"DataMigration:Sources:{source}:BatchSize must be greater than zero when enabled.");
        }
    }
}
