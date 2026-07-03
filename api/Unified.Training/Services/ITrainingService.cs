using Unified.Training.Models;

namespace Unified.Training.Services;

public interface ITrainingService
{
    Task<IReadOnlyCollection<TrainingResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TrainingResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TrainingResponse> CreateAsync(TrainingRequest request, CancellationToken cancellationToken = default);

    Task<TrainingResponse?> UpdateAsync(
        int id,
        TrainingRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
