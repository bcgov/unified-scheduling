namespace Unified.Audit.Models;

/// <summary>Query parameters accepted by GET /api/audit/history.</summary>
public sealed record AuditHistoryQueryParams
{
    /// <summary>Entity type name to scope the query to; always required.</summary>
    public required string EntityType { get; init; }

    /// <summary>Exact match on EntityPK.</summary>
    public string? EntityPK { get; init; }

    /// <summary>Added, Modified, or Deleted.</summary>
    public string? Action { get; init; }

    /// <summary>Column names; matches records where all of these fields appear in ChangedColumns.</summary>
    public List<string>? ChangedField { get; init; }

    public Guid? ActorUserId { get; init; }

    /// <summary>Case-insensitive partial match on ActorName.</summary>
    public string? ActorName { get; init; }

    /// <summary>Inclusive lower bound on OccurredOn; always required.</summary>
    public required DateTimeOffset From { get; init; }

    /// <summary>Inclusive upper bound on OccurredOn; always required.</summary>
    public required DateTimeOffset To { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    /// <summary>asc or desc; always sorted by OccurredOn; defaults to desc.</summary>
    public string? SortDirection { get; init; }
}
