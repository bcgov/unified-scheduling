namespace Unified.Training.Services;

internal class TrainingService : ITrainingService
{
    public static string ModuleName => "Training";

    public string CheckHealth() => $"{ModuleName} Loaded Successfully";
}
