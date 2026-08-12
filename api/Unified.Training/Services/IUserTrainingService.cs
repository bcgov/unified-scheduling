using Unified.Training.Models;

namespace Unified.Training.Services;

public interface IUserTrainingService
{
    Task<IReadOnlyCollection<UserTrainingResponse>> GetUserTrainings(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<UserTrainingResponse?> GetByTrainingAndUserAsync(
        int trainingId,
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<UserTrainingResponse> CreateAsync(UserTrainingRequest request, CancellationToken cancellationToken = default);

    Task<UserTrainingResponse?> UpdateAsync(
        int id,
        UserTrainingRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
