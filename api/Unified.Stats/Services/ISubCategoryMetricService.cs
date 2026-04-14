using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface ISubCategoryMetricService
{
    Task<IReadOnlyCollection<SubCategoryMetricResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SubCategoryMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
