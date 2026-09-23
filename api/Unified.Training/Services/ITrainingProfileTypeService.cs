using Unified.Training.Models;

namespace Unified.Training.Services;

public interface ITrainingProfileTypeService
{
    Task<IReadOnlyCollection<TrainingProfileTypeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
