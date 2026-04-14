using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface ISubCategoryService
{
    Task<IReadOnlyCollection<SubCategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SubCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
