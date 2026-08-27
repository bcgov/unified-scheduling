using System.Text.Json;

namespace Unified.Audit.Models;

public sealed record AuditRecordResponseDto
{
    public required long Id { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required string TableName { get; init; }
    public required JsonElement KeyValues { get; init; }
    public JsonElement? OldValues { get; init; }
    public JsonElement? NewValues { get; init; }
    public IReadOnlyList<string>? ChangedColumns { get; init; }
    public string? SourceModule { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record AuditHistoryResponse
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required IReadOnlyList<AuditRecordResponseDto> Data { get; init; }
}
