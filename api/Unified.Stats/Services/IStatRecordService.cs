using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IStatRecordService
{
    Task<IReadOnlyCollection<StatRecordResponse>> GetAllAsync(
        StatRecordQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );
    Task<StatRecordResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<StatRecordResponse> CreateAsync(
        StatRecordRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(
        IReadOnlyList<StatRecordRequest> requests,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    );
    Task<StatRecordResponse?> UpdateAsync(
        int id,
        StatRecordRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    );
    Task<bool> DeleteAsync(int id, Guid callerUserId, bool callerCanEnterForOthers, CancellationToken cancellationToken = default);
}
