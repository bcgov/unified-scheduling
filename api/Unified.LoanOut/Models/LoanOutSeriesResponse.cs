using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.LoanOut.Models;

/// <summary>
/// Loan out series response built directly on the common
/// <see cref="CalendarEventSeriesResponse"/> contract so it mirrors the shape
/// produced through <see cref="Unified.Calendar.Services.CalendarEventSeriesService{TRequest,TEntity}"/>.
/// </summary>
public sealed record LoanOutSeriesResponse : CalendarEventSeriesResponse
{
    /// <summary>The receiving location configured for the series.</summary>
    public int LoanOutLocationId { get; init; }

    /// <summary>The identifiers of persisted loan-out occurrences belonging to this series.</summary>
    public IReadOnlyCollection<int> LoanOutEntryIds { get; init; } = [];
}
