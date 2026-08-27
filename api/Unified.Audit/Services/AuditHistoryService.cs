using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.Audit.Models;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Audit.Services;

public sealed class AuditHistoryService(UnifiedDbContext DB, TimeProvider timeProvider) : IAuditHistoryService
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

        var (defaultFrom, defaultTo) = AuditDateRangeDefaults.GetCurrentWeekUtc(timeProvider.GetUtcNow());
        var from = queryParams.From ?? defaultFrom;
        var to = queryParams.To ?? defaultTo;

        var query = DB.AuditRecords.AsNoTracking().Where(r => r.OccurredOn >= from && r.OccurredOn <= to);

        if (queryParams.EntityType is { Length: > 0 } entityType)
        {
            query = query.Where(r => r.EntityType == entityType);
        }

        if (queryParams.Action is { Length: > 0 } action)
        {
            query = query.Where(r => r.Action == action);
        }

        if (queryParams.ChangedField is { Length: > 0 } changedField)
        {
            // Translated by the Npgsql provider to `@changedField = ANY("ChangedColumns")`.
            query = query.Where(r => r.ChangedColumns != null && r.ChangedColumns.Contains(changedField));
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

        query = sortAscending ? query.OrderBy(r => r.OccurredOn) : query.OrderByDescending(r => r.OccurredOn);

        // entityKey containment is matched client-side (see MatchesEntityKey) so it behaves identically
        // against Postgres and the in-memory test provider; the other filters above still narrow the
        // candidate set at the database before this runs.
        if (queryParams.EntityKey is { Length: > 0 } entityKeyJson)
        {
            var keyFilter = ParseEntityKeyFilter(entityKeyJson);
            var candidates = await query.ToListAsync(cancellationToken);
            var filtered = candidates.Where(r => MatchesEntityKey(r.KeyValues, keyFilter)).ToList();

            var pageOfFiltered = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(MapToResponse).ToList();

            return new AuditHistoryResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = filtered.Count,
                Data = pageOfFiltered,
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageOfRecords = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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

    private static Dictionary<string, JsonElement> ParseEntityKeyFilter(string entityKeyJson)
    {
        using var document = JsonDocument.Parse(entityKeyJson);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static bool MatchesEntityKey(string keyValuesJson, IReadOnlyDictionary<string, JsonElement> filter)
    {
        using var document = JsonDocument.Parse(keyValuesJson);
        var root = document.RootElement;

        foreach (var (key, expected) in filter)
        {
            if (!root.TryGetProperty(key, out var actual) || !JsonElementValuesEqual(actual, expected))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonElementValuesEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
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
            KeyValues = ParseJson(record.KeyValues) ?? default,
            OldValues = ParseJson(record.OldValues),
            NewValues = ParseJson(record.NewValues),
            ChangedColumns = record.ChangedColumns,
            SourceModule = record.SourceModule,
            CorrelationId = record.CorrelationId,
        };

    private static JsonElement? ParseJson(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
