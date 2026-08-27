using Unified.Audit.Models;

namespace Unified.Audit.Services;

public interface IAuditHistoryService
{
    Task<AuditHistoryResponse> GetHistoryAsync(
        AuditHistoryQueryParams queryParams,
        CancellationToken cancellationToken = default
    );

    /// <summary>Distinct entity types that have at least one audit record, sorted alphabetically.</summary>
    Task<IReadOnlyList<string>> GetRecordedEntityTypesAsync(CancellationToken cancellationToken = default);
}
