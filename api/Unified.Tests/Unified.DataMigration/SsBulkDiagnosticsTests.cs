using Unified.DataMigration.Services;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class SsBulkDiagnosticsTests
{
    [Theory]
    [InlineData("BulkRequiresPerRow:GraphReplacement")]
    [InlineData("BulkRequiresPerRow:TargetChangedDuringBulkRun")]
    [InlineData("BulkRequiresPerRow:TrainingRunInProgress")]
    [InlineData("BulkRequiresPerRow:SchedulingBusinessValidatorsRegistered")]
    public void Findings_ExposeBulkSchedulingRefusalReasons(string reason)
    {
        var runId = Guid.NewGuid().ToString();
        var finding = MigrationDiagnostics.Finding(
            new DataMigrationOutcome
            {
                EntityType = "SchedulingBulk",
                LegacyId = runId,
                Outcome = "Failed",
                Reason = reason,
            }
        );

        Assert.Equal("SchedulingBulk", finding.Entity);
        Assert.Equal(runId, finding.Key);
        Assert.Equal("Failed", finding.Outcome);
        Assert.Equal(reason, finding.Code);
    }

    [Fact]
    public void BulkRefusalDetail_IsKeptOnlyAsAnOpaqueKey()
    {
        Assert.Equal(
            "BulkRequiresPerRow:OwnedUpdate:" + MigrationDiagnostics.Opaque("ShiftEntry"),
            MigrationDiagnostics.Code("BulkRequiresPerRow:OwnedUpdate:ShiftEntry")
        );
    }

    [Theory]
    [InlineData("BulkRequiresPerRow:Something free text")]
    [InlineData("BulkRequiresPerRow:")]
    [InlineData("Prefix BulkRequiresPerRow:GraphReplacement")]
    public void UnknownBulkRefusalReasons_StayOpaque(string reason) =>
        Assert.Equal(MigrationDiagnostics.Opaque(reason), MigrationDiagnostics.Code(reason));
}
