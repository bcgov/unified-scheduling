namespace Unified.LoanOut.Services;

using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

public interface ILoanOutEntryService
{
    Task<LoanOutEntryResponse?> GetLoanOutEntryByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanOutEntryResponse>> GetLoanOutEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<LoanOutEntryResponse> CreateLoanOutEntryAsync(
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<LoanOutEntryResponse?> UpdateLoanOutEntryAsync(
        int id,
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteLoanOutEntryAsync(int id, CancellationToken cancellationToken = default);
}
