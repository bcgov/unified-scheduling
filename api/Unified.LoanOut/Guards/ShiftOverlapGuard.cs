using Unified.Common.Validation;
using Unified.Db.Models.LoanOut;
using Unified.Scheduling.Services;

namespace Unified.LoanOut.Guards;

public static class ShiftOverlapGuard
{
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
