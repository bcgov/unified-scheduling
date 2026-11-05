using Unified.Db.Models.LoanOut;

namespace Unified.LoanOut.Models;

public sealed record LoanOutSeriesMaterializationContext
{
    public required LoanOutSeries LoanOutSeries { get; init; }

    /// <summary>Users assigned to each materialized entry; explicit so it does not depend on series user sync ordering.</summary>
    public required IReadOnlyCollection<Guid> UserIds { get; init; }

    public IReadOnlyCollection<LoanOutEntry> ExistingEntries { get; init; } = [];
}
