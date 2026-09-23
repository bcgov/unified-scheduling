using Unified.Training.Models;

namespace Unified.Training.Services.Lookup;

public interface ITrainingProfileLookupStrategy
{
    Task<IReadOnlyCollection<TrainingProfileLookupResponse>> GetAllAsync(
        bool includeExpired = false,
        CancellationToken cancellationToken = default
    );
}
