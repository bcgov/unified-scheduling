using Unified.Common.Validation;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Scheduling.Services;

namespace Unified.LoanOut.Guards;

public static class ShiftOverlapGuard
{
    public static IReadOnlyCollection<LoanOutEntry> Snapshot(IEnumerable<LoanOutEntry> entries) =>
        entries
            .Select(entry => new LoanOutEntry
            {
                Id = entry.Id,
                LoanOutLocationId = entry.LoanOutLocationId,
                Event = new Event
                {
                    StartAtUtc = entry.Event!.StartAtUtc,
                    EndAtUtc = entry.Event.EndAtUtc,
                    LocationId = entry.Event.LocationId,
                },
                Users = entry.Users.Select(user => new LoanOutEntryUser { UserId = user.UserId }).ToList(),
            })
            .ToList();

    public static async Task<IReadOnlyCollection<LoanOutEntry>> EnsureCanUpdateAsync(
        ISchedulingLoanOutService schedulingLoanOutService,
        IReadOnlyCollection<LoanOutEntry> previousEntries,
        IReadOnlyCollection<LoanOutEntry> updatedEntries,
        CancellationToken cancellationToken = default
    )
    {
        // Destination shifts remain valid wherever the edited loan-out still covers the same user and location.
        var withdrawn = new List<LoanOutEntry>();
        foreach (var previous in previousEntries)
        foreach (var user in previous.Users)
        {
            var remaining = new List<(DateTimeOffset Start, DateTimeOffset End)>
            {
                (previous.Event!.StartAtUtc, previous.Event.EndAtUtc ?? previous.Event.StartAtUtc.AddDays(1)),
            };
            foreach (
                var updated in updatedEntries.Where(entry =>
                    entry.LoanOutLocationId == previous.LoanOutLocationId
                    && entry.Users.Any(candidate => candidate.UserId == user.UserId)
                )
            )
            {
                var start = updated.Event!.StartAtUtc;
                var end = updated.Event.EndAtUtc ?? start.AddDays(1);
                var uncovered = new List<(DateTimeOffset Start, DateTimeOffset End)>();
                foreach (var range in remaining)
                {
                    if (end <= range.Start || start >= range.End)
                        uncovered.Add(range);
                    else
                    {
                        if (start > range.Start)
                            uncovered.Add((range.Start, start));
                        if (end < range.End)
                            uncovered.Add((end, range.End));
                    }
                }
                remaining = uncovered;
            }
            withdrawn.AddRange(
                remaining.Select(range => new LoanOutEntry
                {
                    Id = previous.Id,
                    LoanOutLocationId = previous.LoanOutLocationId,
                    Event = new Event
                    {
                        StartAtUtc = range.Start,
                        EndAtUtc = range.End,
                        LocationId = previous.Event.LocationId,
                    },
                    Users = [new LoanOutEntryUser { UserId = user.UserId }],
                })
            );
        }
        foreach (var group in withdrawn.GroupBy(entry => entry.LoanOutLocationId))
            await EnsureNoShiftAtLoanOutLocationAsync(schedulingLoanOutService, group, group.Key, cancellationToken);
        return withdrawn;
    }

    public static async Task EnsureNoShiftAtLoanOutLocationAsync(
        ISchedulingLoanOutService schedulingLoanOutService,
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            await schedulingLoanOutService.HasShiftAtLoanOutLocationAsync(
                loanOutEntries,
                loanOutLocationId,
                cancellationToken
            )
        )
            throw new ConflictValidationException(
                new Dictionary<string, string[]>
                {
                    ["LoanOut"] =
                    [
                        "A shift exists at the loan-out location, so the loan-out cannot be updated or removed.",
                    ],
                }
            );
    }
}
