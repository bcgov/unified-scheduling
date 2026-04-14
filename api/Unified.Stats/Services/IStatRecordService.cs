using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatRecordService
{
    Task<IReadOnlyCollection<StatRecordResponse>> GetAllAsync(StatRecordQueryParams? queryParams = null, CancellationToken cancellationToken = default);
    Task<StatRecordResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<StatRecordResponse> CreateAsync(StatRecordRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(IEnumerable<StatRecordRequest> requests, CancellationToken cancellationToken = default);
    Task<StatRecordResponse?> UpdateAsync(int id, StatRecordRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GenerateTestDataAsync(int count, CancellationToken cancellationToken = default);
}
