namespace Unified.Audit.Models;

public sealed record AuditEntityTypesResponse
{
    public required IReadOnlyList<string> EntityTypes { get; init; }
}

/// <summary>One auditable field on an entity type, as exposed to the filter-dropdown / diff-panel UI.</summary>
public sealed record AuditEntityFieldDto
{
    /// <summary>Raw property/column name as it appears in ChangedColumns.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable display label.</summary>
    public required string Label { get; init; }

    /// <summary>One of string, number, boolean, date, uuid.</summary>
    public required string Type { get; init; }
}

public sealed record AuditEntityFieldsResponse
{
    public required string EntityType { get; init; }
    public required IReadOnlyList<AuditEntityFieldDto> Fields { get; init; }
}
