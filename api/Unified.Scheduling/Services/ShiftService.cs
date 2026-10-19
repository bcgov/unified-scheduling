using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftService(IShiftEntryService entryService, IShiftSeriesService seriesService)
    : IShiftService,
        IShiftPublicationService
{
    public Task<IReadOnlyCollection<ShiftSeriesResponse>> GetShiftSeriesAsync(
        ShiftSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    ) => seriesService.GetShiftSeriesAsync(queryParams, cancellationToken);

    public Task<ShiftSeriesResponse?> GetShiftSeriesByIdAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.GetShiftSeriesByIdAsync(id, cancellationToken);

    public Task<ShiftSeriesResponse> CreateShiftSeriesAsync(
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.CreateShiftSeriesAsync(request, cancellationToken);

    public Task<ShiftSeriesResponse?> UpdateShiftSeriesAsync(
        int id,
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.UpdateShiftSeriesAsync(id, request, cancellationToken);

    public Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.PublishShiftSeriesAsync(id, cancellationToken);

    public Task<ShiftSeriesResponse?> ExpireShiftSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    ) => seriesService.ExpireShiftSeriesAsync(id, request, cancelledByUserId, cancellationToken);

    public Task<bool> DeleteShiftSeriesAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.DeleteShiftSeriesAsync(id, cancellationToken);

    public Task<ShiftSeries?> GetShiftSeriesEntityForEntryAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => seriesService.GetShiftSeriesEntityForEntryAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<ShiftEntryResponse>> GetShiftEntriesAsync(
        ShiftEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    ) => entryService.GetShiftEntriesAsync(queryParams, cancellationToken);

    public Task<ShiftEntryResponse?> GetShiftEntryByIdAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.GetShiftEntryByIdAsync(id, cancellationToken);

    public Task<ShiftEntryResponse> CreateShiftEntryAsync(
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.CreateShiftEntryAsync(request, cancellationToken);

    public Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
        int id,
        ShiftEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    ) => entryService.UpdateShiftEntryAsync(id, request, cancellationToken, conflictOverrideActorId);

    public Task<ShiftEntryResponse?> PublishShiftEntryAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.PublishShiftEntryAsync(id, cancellationToken);

    public Task<ShiftEntryResponse?> ExpireShiftEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    ) => entryService.ExpireShiftEntryAsync(id, request, cancelledByUserId, cancellationToken);

    public Task<bool> DeleteShiftEntryAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.DeleteShiftEntryAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<ShiftPublicationBlocker>> GetEntryPublicationBlockersAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    ) => ((IShiftPublicationService)entryService).GetEntryPublicationBlockersAsync(shiftEntryIds, cancellationToken);

    public Task<ShiftPublicationResult> PublishEntriesAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    ) => ((IShiftPublicationService)entryService).PublishEntriesAsync(shiftEntryIds, cancellationToken);
}
