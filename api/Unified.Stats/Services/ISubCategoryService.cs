using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface ISubCategoryService
{
    Task<IReadOnlyCollection<SubCategoryResponse>> GetAllAsync(
        int? categoryId = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    );
    Task<SubCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
