using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.LoanOut.Models;

/// <summary>
/// Loan out entry response built directly on the common <see cref="CalendarEventEntryResponse"/>
/// contract so it mirrors the shape produced through
/// <see cref="Unified.Calendar.Services.CalendarEventEntryService{TRequest,TEntity}"/>.
/// </summary>
public sealed record LoanOutEntryResponse : CalendarEventEntryResponse
{
    /// <summary>The location receiving the loaned-out users.</summary>
    public int LoanOutLocationId { get; init; }

    /// <summary>The parent loan-out series identifier, or null for a standalone entry.</summary>
    public int? LoanOutSeriesId { get; init; }
}
