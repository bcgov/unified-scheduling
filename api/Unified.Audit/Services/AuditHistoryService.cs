using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.Audit.Models;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Audit.Services;

public sealed class AuditHistoryService(UnifiedDbContext DB) : IAuditHistoryService
{
    private const int DefaultPageSize = 25;

    public async Task<AuditHistoryResponse> GetHistoryAsync(
        AuditHistoryQueryParams queryParams,
        CancellationToken cancellationToken = default
    )
    {
        var page = queryParams.Page is > 0 ? queryParams.Page.Value : 1;
        var pageSize = queryParams.PageSize is > 0 ? queryParams.PageSize.Value : DefaultPageSize;
        var sortAscending = string.Equals(queryParams.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var query = DB
            .AuditRecords.AsNoTracking()
            .Where(r => r.OccurredOn >= queryParams.From && r.OccurredOn <= queryParams.To);

        if (queryParams.EntityType is { Length: > 0 } entityType)
        {
            query = query.Where(r => r.EntityType == entityType);
        }

        if (queryParams.EntityPK is { Length: > 0 } entityPK)
        {
            query = query.Where(r => r.EntityPK == entityPK);
        }

        if (queryParams.Action is { Length: > 0 } action)
        {
            query = query.Where(r => r.Action == action);
        }

        if (queryParams.ChangedField is { Count: > 0 } changedFields)
        {
            // One Where per field (rather than changedFields.All(...) inside a single Where) so each
            // clause is translated the same way as the single-field case by both the Npgsql provider
            // and the in-memory test provider. Chaining ANDs the clauses, so every field must match.
            foreach (var changedField in changedFields)
            {
                query = query.Where(r => r.ChangedColumns != null && r.ChangedColumns.Contains(changedField));
            }
        }

        if (queryParams.ActorUserId is { } actorUserId)
        {
            query = query.Where(r => r.ActorUserId == actorUserId);
        }

        if (queryParams.ActorName is { Length: > 0 } actorName)
        {
            var normalized = actorName.ToLowerInvariant();
            query = query.Where(r => r.ActorName != null && r.ActorName.ToLower().Contains(normalized));
        }

        query = sortAscending
            ? query.OrderBy(r => r.OccurredOn).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.OccurredOn).ThenByDescending(r => r.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageOfRecords = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new AuditHistoryResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Data = [.. pageOfRecords.Select(MapToResponse)],
        };
    }

    public async Task<IReadOnlyList<string>> GetRecordedEntityTypesAsync(CancellationToken cancellationToken = default)
    {
        return await DB
            .AuditRecords.AsNoTracking()
            .Select(r => r.EntityType)
            .Distinct()
            .OrderBy(entityType => entityType)
            .ToListAsync(cancellationToken);
    }

    private static AuditRecordResponseDto MapToResponse(AuditRecord record) =>
        new()
        {
            Id = record.Id,
            OccurredOn = record.OccurredOn,
            ActorUserId = record.ActorUserId,
            ActorName = record.ActorName,
            Action = record.Action,
            EntityType = record.EntityType,
            TableName = record.TableName,
            EntityPK = record.EntityPK,
            OldValues = ParseJson(record.OldValues),
            NewValues = ParseJson(record.NewValues),
            ChangedColumns = record.ChangedColumns,
            CorrelationId = record.CorrelationId,
        };

    private static Dictionary<string, object?>? ParseJson(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
}
