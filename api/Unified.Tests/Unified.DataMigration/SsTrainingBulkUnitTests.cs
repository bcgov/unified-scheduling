using System.Reflection;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Services.Bulk;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class SsTrainingBulkUnitTests
{
    private readonly DataMigrationOptionsValidator _validator = new();

    private static DataMigrationOptions Enabled(Action<SsJobOptions> configure)
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
                    CutoverDate = new DateOnly(2026, 12, 1),
                },
            },
        };
        configure(options.Sources.SS.Jobs);
        return options;
    }

    [Fact]
    public void Validate_DefaultPersistenceMode_IsPerRow()
    {
        var job = new MigrationJobOptions();

        Assert.Equal(MigrationPersistenceMode.PerRow, job.PersistenceMode);
        Assert.Equal(2000, job.BulkChunkSize);
    }

    [Theory]
    [InlineData("Training")]
    [InlineData("Scheduling")]
    public void Validate_BulkTrainingOrScheduling_Succeeds(string job)
    {
        var result = _validator.Validate(
            null,
            Enabled(jobs =>
            {
                jobs.For(job).Enabled = true;
                jobs.For(job).PersistenceMode = MigrationPersistenceMode.Bulk;
            })
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("MyTeam", true)]
    [InlineData("MyTeam", false)]
    [InlineData("Report", false)]
    public void Validate_BulkOnUnsupportedJob_FailsEvenWhenDisabled(string job, bool enabled)
    {
        var result = _validator.Validate(
            null,
            Enabled(jobs =>
            {
                jobs.For(job).Enabled = enabled;
                jobs.For(job).PersistenceMode = MigrationPersistenceMode.Bulk;
            })
        );

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains($"SS:{job}:PersistenceMode Bulk", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UndefinedPersistenceModeAndNonPositiveChunk_Fail()
    {
        var result = _validator.Validate(
            null,
            Enabled(jobs =>
            {
                jobs.Training.Enabled = true;
                jobs.Training.PersistenceMode = (MigrationPersistenceMode)42;
                jobs.Training.BulkChunkSize = 0;
            })
        );

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            x => x.Contains("Training:PersistenceMode is invalid", StringComparison.Ordinal)
        );
        Assert.Contains(result.Failures, x => x.Contains("Training:BulkChunkSize", StringComparison.Ordinal));
    }

    [Fact]
    public void SafeTargetFields_MatchPerRowWriteSession()
    {
        Assert.Equal(
            PrivateSet("SafeTargetFields").Order(StringComparer.Ordinal),
            SsBulkTargets.SafeTargetFields.Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void PrimaryTypes_MatchPerRowWriteSession()
    {
        Assert.Equal(
            PrivateSet("PrimaryTypes").Order(StringComparer.Ordinal),
            SsBulkLedger.PrimaryTypes.Order(StringComparer.Ordinal)
        );
    }

    [Theory]
    [InlineData(null, MigrationPersistenceMode.PerRow, true)] // Evidence written before the setting existed.
    [InlineData(null, MigrationPersistenceMode.Bulk, false)]
    [InlineData("PerRow", MigrationPersistenceMode.PerRow, true)]
    [InlineData("PerRow", MigrationPersistenceMode.Bulk, false)]
    [InlineData("Bulk", MigrationPersistenceMode.Bulk, true)]
    [InlineData("Bulk", MigrationPersistenceMode.PerRow, false)]
    public void TriggerEvidence_BindsPersistenceMode(string? recorded, MigrationPersistenceMode mode, bool expected)
    {
        var run = new DataMigrationRun
        {
            Id = Guid.NewGuid(),
            Source = "SS",
            Job = "Training",
            Status = "Queued",
        };
        var suffix = recorded is null ? "" : $",\"PersistenceMode\":\"{recorded}\"";
        var json =
            $"{{\"Source\":\"SS\",\"Job\":\"Training\",\"RunId\":\"{run.Id}\",\"Operation\":\"Trigger\","
            + $"\"DryRun\":false{suffix}}}";

        Assert.True(SsMigrationExecutor.IsTriggerEvidence(json, run));
        Assert.Equal(expected, SsMigrationExecutor.IsTriggerEvidence(json, run, mode));
        Assert.False(SsMigrationExecutor.IsTriggerEvidence("not json", run, mode));
    }

    private static IEnumerable<string> PrivateSet(string name) =>
        (IEnumerable<string>)
            typeof(SsWriteSession).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
}
