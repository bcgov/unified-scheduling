using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Services;

public interface ILoanOutSeriesService
{
    Task<IReadOnlyList<LoanOutSeriesResponse>> GetLoanOutSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<LoanOutSeriesResponse?> GetLoanOutSeriesByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<LoanOutSeriesResponse> CreateLoanOutSeriesAsync(
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<LoanOutSeriesResponse?> UpdateLoanOutSeriesAsync(
        int id,
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteLoanOutSeriesAsync(int id, CancellationToken cancellationToken = default);
}
