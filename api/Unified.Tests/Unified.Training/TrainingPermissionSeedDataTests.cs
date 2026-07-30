using Unified.Authorization;
using Unified.Training;

namespace Unified.Tests.Training;

public sealed class TrainingPermissionSeedDataTests
{
    [Fact]
    public void Definitions_Should_Include_UserTraining_Permissions()
    {
        var ids = TrainingPermissionSeedData.Instance.Definitions.Select(x => x.Id).ToArray();

        Assert.Contains(nameof(Permissions.TrainingsView), ids);
        Assert.Contains(nameof(Permissions.TrainingsCreate), ids);
        Assert.Contains(nameof(Permissions.TrainingsEdit), ids);
        Assert.Contains(nameof(Permissions.UserTrainingsView), ids);
        Assert.Contains(nameof(Permissions.UserTrainingsCreate), ids);
        Assert.Contains(nameof(Permissions.UserTrainingsEdit), ids);
        Assert.Contains(nameof(Permissions.UserTrainingsDelete), ids);
        Assert.Equal(7, ids.Length);
    }
}
