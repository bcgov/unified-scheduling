using Unified.DataMigration.Services;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationOrchestratorTests
{
    [Fact]
    public void PayloadHash_IsStableAndDoesNotRetainPayload()
    {
        var hash = DataMigrationOrchestrator.ComputePayloadHash("source payload");

        Assert.Equal(hash, DataMigrationOrchestrator.ComputePayloadHash("source payload"));
        Assert.NotEqual(hash, DataMigrationOrchestrator.ComputePayloadHash("changed payload"));
        Assert.False(hash.Contains("source payload", StringComparison.Ordinal));
    }

    [Fact]
    public void IsAfterWatermark_UsesTimestampThenLegacyId()
    {
        var at = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var watermark = new DataMigrationWatermark
        {
            Source = "SS",
            EntityType = "User",
            SourceChangedAtUtc = at,
            LegacyId = "10",
        };

        Assert.True(DataMigrationOrchestrator.IsAfterWatermark(at, "11", watermark));
        Assert.False(DataMigrationOrchestrator.IsAfterWatermark(at, "10", watermark));
        Assert.False(DataMigrationOrchestrator.IsAfterWatermark(at, "09", watermark));
        Assert.True(DataMigrationOrchestrator.IsAfterWatermark(at.AddTicks(1), "01", watermark));
    }
}
