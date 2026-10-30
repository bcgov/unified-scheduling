using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.LoanOut.Models;

/// <summary>
/// Loan-out series create or update request built on the common
/// <see cref="CalendarEventSeriesRequest"/> contract so it can be built through
/// <see cref="Unified.Calendar.Services.CalendarEventSeriesService{TRequest,TEntity}"/>.
/// </summary>
public record LoanOutSeriesRequest : CalendarEventSeriesRequest
{
    /// <summary>The positive identifier of the receiving location copied to materialized occurrences.</summary>
    public required int LoanOutLocationId { get; init; }
}
