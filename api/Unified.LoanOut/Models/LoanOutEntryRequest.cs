using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.LoanOut.Models;

/// <summary>
/// Loan-out entry create or update request built on the common <see cref="CalendarEventEntryRequest"/>
/// contract so it can be built through <see cref="Unified.Calendar.Services.CalendarEventEntryService{TRequest,TEntity}"/>.
/// </summary>
public record LoanOutEntryRequest : CalendarEventEntryRequest
{
    /// <summary>The positive identifier of the location receiving the loaned-out users.</summary>
    public required int LoanOutLocationId { get; init; }

    /// <summary>The parent loan-out series identifier, or null for a standalone entry.</summary>
    public int? LoanOutSeriesId { get; init; }
}
