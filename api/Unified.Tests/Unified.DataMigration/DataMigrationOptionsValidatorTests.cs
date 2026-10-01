using Unified.DataMigration.Options;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationOptionsValidatorTests
{
    private readonly DataMigrationOptionsValidator _validator = new();

    [Fact]
    public void Validate_DisabledSources_DoNotRequireSecretsOrSchedules()
    {
        var result = _validator.Validate(null, new DataMigrationOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledSource_RequiresOnlyItsOwnValidSettings()
    {
        var options = new DataMigrationOptions
        {
            Sources = new DataMigrationSourcesOptions
            {
                SS = new DataMigrationSourceOptions
                {
                    Enabled = true,
                    ConnectionString = "Host=source",
                    CronSchedule = "0 * * * *",
                    BatchSize = 10,
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledSourceWithMissingSettings_Fails()
    {
        var result = _validator.Validate(
            null,
            new DataMigrationOptions
            {
                Sources = new DataMigrationSourcesOptions { CASS = new DataMigrationSourceOptions { Enabled = true } },
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("CASS", StringComparison.Ordinal));
    }
}
