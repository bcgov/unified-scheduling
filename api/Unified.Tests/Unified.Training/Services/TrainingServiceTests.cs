using Unified.Training.Services;

namespace Unified.Tests.Training.Services;

public class TrainingServiceTests
{
    [Fact]
    public void ModuleName_Should_Return_Training()
    {
        var service = new TrainingService();
        var result = service.ModuleName;

        Assert.Equal("Training", result);
    }

    [Fact]
    public void CheckHealth_Should_Return_Success_Message()
    {
        var service = new TrainingService();
        var result = service.CheckHealth();

        Assert.Equal("Training Loaded Successfully", result);
    }
}
