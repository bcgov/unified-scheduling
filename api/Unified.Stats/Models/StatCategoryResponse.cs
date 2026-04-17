namespace Unified.Stats.Models;

public sealed record StatCategoryResponse(
    int Id,
    int GroupId,
    string Name,
    bool IsArchived,
    bool IsHighSecurity,
    int DisplayOrder
);
