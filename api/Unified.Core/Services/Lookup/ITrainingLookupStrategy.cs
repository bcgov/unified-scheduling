using Unified.Core.Models;

namespace Unified.Core.Services.Lookup;

public interface ITrainingLookupStrategy : ILookupStrategy
{
    Task<IReadOnlyCollection<TrainingLookupResponse>> GetAllTrainingsAsync(CancellationToken cancellationToken = default);

    Task<TrainingLookupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TrainingLookupResponse> CreateAsync(TrainingLookupRequest request, CancellationToken cancellationToken = default);

    Task<TrainingLookupResponse?> UpdateAsync(
        int id,
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    );

    Task<TrainingLookupResponse?> MoveOrderAsync(
        int id,
        int newOrder,
        CancellationToken cancellationToken = default
    );
}