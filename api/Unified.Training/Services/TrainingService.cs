namespace Unified.Training.Services;

public class TrainingService : ITrainingService
{
    public string ModuleName => "Training";

    public string CheckHealth() => $"{ModuleName} Loaded Successfully";
}
