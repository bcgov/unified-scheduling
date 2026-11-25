using System.Reflection;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Services.Bulk;
using Unified.DataMigration.Sources;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class SsTrainingBulkUnitTests
{
    private readonly DataMigrationOptionsValidator _validator = new();

    [Fact]
    public void SourceFingerprint_IsOrderIndependent_AndTracksProjectedInput_NotPhotoBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = new SsRow(
            new Dictionary<string, object?>
            {
                ["Id"] = "1",
                ["FirstName"] = "One",
                ["Photo"] = new byte[] { 1 },
            }
        );
        var second = new SsRow(new Dictionary<string, object?> { ["Id"] = "2", ["FirstName"] = "Two" });
        var course = new SsRow(new Dictionary<string, object?> { ["Id"] = "3", ["Code"] = "Course" });
        var snapshot = new Dictionary<string, List<SsRow>> { ["User"] = [first, second], ["LookupCode"] = [course] };
        var original = SsMigrationFingerprints.Source(snapshot, ct);
        var reordered = new Dictionary<string, List<SsRow>>
        {
            ["LookupCode"] = [course],
            ["User"] =
            [
                second,
                new SsRow(
                    new Dictionary<string, object?>
                    {
                        ["Photo"] = new byte[] { 2, 3 },
                        ["FirstName"] = "One",
                        ["Id"] = "1",
                    }
                ),
            ],
        };
        Assert.Equal(original, SsMigrationFingerprints.Source(reordered, ct));
        Assert.Equal(3, original.Rows);
        Assert.Matches("^[A-F0-9]{64}$", original.Value);

        reordered["LookupCode"] =
        [
            new SsRow(new Dictionary<string, object?> { ["Id"] = "3", ["Code"] = "Changed course" }),
        ];
        Assert.NotEqual(original.Value, SsMigrationFingerprints.Source(reordered, ct).Value);
        reordered["LookupCode"] = [course];
        reordered["User"][1] = new SsRow(
            new Dictionary<string, object?>
            {
                ["Id"] = "1",
                ["FirstName"] = "One",
                ["Photo"] = null,
            }
        );
        Assert.NotEqual(original.Value, SsMigrationFingerprints.Source(reordered, ct).Value);
    }

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
    public void IncludeExcused_DefaultsFalse_AndIsAcceptedOnlyForTraining()
    {
        Assert.False(new MigrationJobOptions().IncludeExcused);
        Assert.True(_validator.Validate(null, Enabled(jobs => jobs.Training.IncludeExcused = true)).Succeeded);
        foreach (var job in new[] { "MyTeam", "Scheduling", "Report" })
        {
            var result = _validator.Validate(null, Enabled(jobs => jobs.For(job).IncludeExcused = true));
            Assert.True(result.Failed);
            Assert.Contains(result.Failures, x => x.Contains($"SS:{job}:IncludeExcused", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ExcusedSheriff_ExcludedOnlyWhenIncludeExcusedIsOff_AndRepeatsFollowTheSwitch()
    {
        var excused = new SsRow(new Dictionary<string, object?> { ["Id"] = "u1", ["Excused"] = true });
        var active = new SsRow(new Dictionary<string, object?> { ["Id"] = "u2", ["Excused"] = false });
        Assert.True(SsTrainingRules.ExcludedExcused(excused, includeExcused: false));
        Assert.False(SsTrainingRules.ExcludedExcused(excused, includeExcused: true));
        Assert.False(SsTrainingRules.ExcludedExcused(active, includeExcused: false));
        Assert.Equal("ExcusedSheriff", SsTrainingRules.ExcludedHistoryReason(linked: false, excused: true));

        var sheriff = Guid.NewGuid();
        var users = new Dictionary<string, SsRow>
        {
            [sheriff.ToString()] = new(
                new Dictionary<string, object?> { ["Id"] = sheriff.ToString(), ["Excused"] = true }
            ),
        };
        // Pre-2020 repeats count too: Training has no history date limit.
        SsRow History(int id, int year) =>
            new(
                new Dictionary<string, object?>
                {
                    ["Id"] = id.ToString(),
                    ["SheriffId"] = sheriff,
                    ["TrainingTypeId"] = 7,
                    ["EndDate"] = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ["ExpiryDate"] = null,
                }
            );
        List<SsRow> histories = [History(1, 2009), History(2, 2010)];
        Assert.Empty(SsTrainingRules.RepeatedCourses(histories, users, includeExcused: false));
        Assert.Equal(new int?[] { 7 }, SsTrainingRules.RepeatedCourses(histories, users, includeExcused: true));
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
