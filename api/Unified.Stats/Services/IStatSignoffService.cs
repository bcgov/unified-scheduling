using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatSignoffService
{
    Task<IReadOnlyCollection<StatSignoffResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StatSignoffResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<StatSignoffResponse> CreateAsync(StatSignoffRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
