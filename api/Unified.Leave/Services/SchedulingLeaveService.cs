using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

public sealed class SchedulingLeaveService(
    ISchedulingLeaveEntryService entryService,
    ISchedulingLeaveSeriesService seriesService
) : ISchedulingLeaveService
{
    public Task<SchedulingLeaveEntryResponse?> GetLeaveEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => entryService.GetLeaveEntryByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<SchedulingLeaveEntryResponse>> GetLeaveEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => entryService.GetLeaveEntriesByUserAsync(userId, cancellationToken);

    public Task<SchedulingLeaveEntryResponse> CreateLeaveEntryAsync(
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.CreateLeaveEntryAsync(request, cancellationToken);

    public Task<SchedulingLeaveEntryResponse?> UpdateLeaveEntryAsync(
        int id,
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.UpdateLeaveEntryAsync(id, request, cancellationToken);

    public Task<bool> DeleteLeaveEntryAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.DeleteLeaveEntryAsync(id, cancellationToken);

    public Task<IReadOnlyList<SchedulingLeaveSeriesResponse>> GetLeaveSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => seriesService.GetLeaveSeriesByUserAsync(userId, cancellationToken);

    public Task<SchedulingLeaveSeriesResponse?> GetLeaveSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => seriesService.GetLeaveSeriesByIdAsync(id, cancellationToken);

    public Task<SchedulingLeaveSeriesResponse> CreateLeaveSeriesAsync(
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.CreateLeaveSeriesAsync(request, cancellationToken);

    public Task<SchedulingLeaveSeriesResponse?> UpdateLeaveSeriesAsync(
        int id,
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.UpdateLeaveSeriesAsync(id, request, cancellationToken);

    public Task<bool> DeleteLeaveSeriesAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.DeleteLeaveSeriesAsync(id, cancellationToken);
}
