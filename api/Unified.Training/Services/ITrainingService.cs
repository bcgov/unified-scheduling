namespace Unified.Training.Services;

internal interface ITrainingService
{
    string ModuleName { get; }

    string CheckHealth();
}
