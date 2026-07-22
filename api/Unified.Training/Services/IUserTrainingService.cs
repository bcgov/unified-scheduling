using Unified.Training.Models;

namespace Unified.Training.Services;

public interface IUserTrainingService
{
    Task<IReadOnlyCollection<UserTrainingResponse>> GetAllAsync(
        Guid userId,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    );

    Task<UserTrainingResponse?> GetByTrainingAndUserAsync(
        int trainingId,
        Guid userId,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    );

    Task<UserTrainingResponse> CreateAsync(
        UserTrainingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    );

    Task<UserTrainingResponse?> UpdateAsync(
        int id,
        UserTrainingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(int id, Guid callerUserId, CancellationToken cancellationToken = default);
}
