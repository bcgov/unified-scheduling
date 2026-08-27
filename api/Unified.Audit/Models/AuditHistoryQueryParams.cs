namespace Unified.Audit.Models;

/// <summary>Query parameters accepted by GET /api/audit/history.</summary>
public sealed record AuditHistoryQueryParams
{
    public string? EntityType { get; init; }

    /// <summary>JSON-encoded key value, e.g. <c>{"Id":"some-guid"}</c>.</summary>
    public string? EntityKey { get; init; }

    /// <summary>Added, Modified, or Deleted.</summary>
    public string? Action { get; init; }

    /// <summary>Single column name; matches records where this field appears in ChangedColumns.</summary>
    public string? ChangedField { get; init; }

    public Guid? ActorUserId { get; init; }

    /// <summary>Case-insensitive partial match on ActorName.</summary>
    public string? ActorName { get; init; }

    /// <summary>Inclusive lower bound on OccurredOn; defaults to start of current week (UTC).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive upper bound on OccurredOn; defaults to end of current week (UTC).</summary>
    public DateTimeOffset? To { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    /// <summary>asc or desc; always sorted by OccurredOn; defaults to desc.</summary>
    public string? SortDirection { get; init; }
}
