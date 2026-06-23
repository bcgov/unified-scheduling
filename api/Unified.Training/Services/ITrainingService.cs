namespace Unified.Training.Services;

internal interface ITrainingService
{
    static abstract string ModuleName { get; }

    string CheckHealth();
}
