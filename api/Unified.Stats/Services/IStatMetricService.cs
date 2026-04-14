using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatMetricService
{
    Task<IReadOnlyCollection<StatMetricResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StatMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
