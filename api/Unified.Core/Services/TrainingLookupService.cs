using Unified.Core.Models;
using Unified.Core.Services.Lookup;

namespace Unified.Core.Services;

public sealed class TrainingLookupService(ITrainingLookupStrategy strategy) : ITrainingLookupService
{
    public Task<IReadOnlyCollection<TrainingLookupResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    ) => strategy.GetAllTrainingsAsync(cancellationToken);

    public Task<TrainingLookupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        strategy.GetByIdAsync(id, cancellationToken);

    public Task<TrainingLookupResponse> CreateAsync(
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    ) => strategy.CreateAsync(request, cancellationToken);

    public Task<TrainingLookupResponse?> UpdateAsync(
        int id,
        TrainingLookupRequest request,
        CancellationToken cancellationToken = default
    ) => strategy.UpdateAsync(id, request, cancellationToken);

    public Task<TrainingLookupResponse?> MoveOrderAsync(
        int id,
        int newOrder,
        CancellationToken cancellationToken = default
    ) => strategy.MoveOrderAsync(id, newOrder, cancellationToken);
}
