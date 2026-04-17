using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatGroupService
{
    Task<IReadOnlyCollection<StatGroupResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StatGroupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
