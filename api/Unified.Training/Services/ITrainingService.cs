namespace Unified.Training.Services;

public interface ITrainingService
{
    string ModuleName { get; }

    string CheckHealth();
}
