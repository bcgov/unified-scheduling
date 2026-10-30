using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;

namespace Unified.LoanOut.Services;

public sealed class LoanOutService(ILoanOutEntryService entryService, ILoanOutSeriesService seriesService)
    : ILoanOutService
{
    public Task<LoanOutEntryResponse?> GetLoanOutEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => entryService.GetLoanOutEntryByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<LoanOutEntryResponse>> GetLoanOutEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => entryService.GetLoanOutEntriesByUserAsync(userId, cancellationToken);

    public Task<LoanOutEntryResponse> CreateLoanOutEntryAsync(
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.CreateLoanOutEntryAsync(request, cancellationToken);

    public Task<LoanOutEntryResponse?> UpdateLoanOutEntryAsync(
        int id,
        LoanOutEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.UpdateLoanOutEntryAsync(id, request, cancellationToken);

    public Task<bool> DeleteLoanOutEntryAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.DeleteLoanOutEntryAsync(id, cancellationToken);

    public Task<IReadOnlyList<LoanOutSeriesResponse>> GetLoanOutSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => seriesService.GetLoanOutSeriesByUserAsync(userId, cancellationToken);

    public Task<LoanOutSeriesResponse?> GetLoanOutSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => seriesService.GetLoanOutSeriesByIdAsync(id, cancellationToken);

    public Task<LoanOutSeriesResponse> CreateLoanOutSeriesAsync(
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.CreateLoanOutSeriesAsync(request, cancellationToken);

    public Task<LoanOutSeriesResponse?> UpdateLoanOutSeriesAsync(
        int id,
        LoanOutSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.UpdateLoanOutSeriesAsync(id, request, cancellationToken);

    public Task<bool> DeleteLoanOutSeriesAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.DeleteLoanOutSeriesAsync(id, cancellationToken);
}
