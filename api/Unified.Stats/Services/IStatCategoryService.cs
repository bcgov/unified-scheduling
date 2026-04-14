using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatCategoryService
{
    Task<IReadOnlyCollection<StatCategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StatCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
