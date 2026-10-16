using Unified.Db.Models.Leave;

namespace Unified.Leave.Models;

public sealed record LeaveSeriesMaterializationContext
{
    public required LeaveSeries LeaveSeries { get; init; }

    /// <summary>Users assigned to each materialized entry; explicit so it does not depend on series user sync ordering.</summary>
    public required IReadOnlyCollection<Guid> UserIds { get; init; }

    public IReadOnlyCollection<LeaveEntry> ExistingEntries { get; init; } = [];
}
