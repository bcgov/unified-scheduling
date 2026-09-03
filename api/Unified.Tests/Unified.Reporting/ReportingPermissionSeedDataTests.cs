using Unified.Authorization;
using Unified.Reporting;

namespace Unified.Tests.Reporting;

public sealed class ReportingPermissionSeedDataTests
{
    [Fact]
    public void Definitions_Should_Include_ReportsGenerate_Permission()
    {
        var definitions = ReportingPermissionSeedData.Instance.Definitions;

        var definition = Assert.Single(definitions);
        Assert.Equal("Reports", definition.Group);
        Assert.Equal(nameof(Permissions.ReportsGenerate), definition.Id);
        Assert.False(string.IsNullOrWhiteSpace(definition.Description));
    }
}
