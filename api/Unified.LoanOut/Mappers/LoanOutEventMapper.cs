using Unified.Calendar.Mappings;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Mappers;

/// <summary>
/// Maps loan-out entry and series responses using shared calendar field mappings,
/// adding loan-out locations, series links, and occurrence identifiers.
/// </summary>
public sealed class LoanOutEventMapper(CalendarEventMapper calendarEventMapper)
{
    /// <summary>
    /// Maps a loan-out entry and its distinct assigned users to an API response.
    /// </summary>
    public LoanOutEntryResponse ToLoanOutEntryResponse(LoanOutEntry loanOutEntry)
    {
        var userIds = loanOutEntry.Users.Select(user => user.UserId).Distinct().ToList();
        var response = calendarEventMapper.ToEntryResponse(
            () => new LoanOutEntryResponse { Title = string.Empty, StartAtUtc = default },
            loanOutEntry.Event!,
            userIds
        );

        return response with
        {
            Id = loanOutEntry.Id,
            LoanOutLocationId = loanOutEntry.LoanOutLocationId,
            LoanOutSeriesId = loanOutEntry.LoanOutSeriesId,
        };
    }

    public LoanOutSeriesResponse ToLoanOutSeriesResponse(
        LoanOutSeries series,
        IReadOnlyCollection<int> eventIds,
        IReadOnlyCollection<int> loanOutEntryIds
    )
    {
        var userIds = series.Users.Select(user => user.UserId).Distinct().ToList();
        var response = calendarEventMapper.ToSeriesResponse(
            () => new LoanOutSeriesResponse { Title = string.Empty, StartAtUtc = default },
            series.EventSeries!,
            userIds,
            eventIds
        );

        return response with
        {
            Id = series.Id,
            LoanOutLocationId = series.LoanOutLocationId,
            LoanOutEntryIds = loanOutEntryIds,
        };
    }
}
