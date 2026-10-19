using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class AssignmentService(IAssignmentEntryService entryService, IAssignmentSeriesService seriesService)
    : IAssignmentService,
        IAssignmentPublicationService
{
    public Task<IReadOnlyCollection<AssignmentSeriesResponse>> GetAssignmentSeriesAsync(
        AssignmentSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    ) => seriesService.GetAssignmentSeriesAsync(queryParams, cancellationToken);

    public Task<AssignmentSeriesResponse?> GetAssignmentSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => seriesService.GetAssignmentSeriesByIdAsync(id, cancellationToken);

    public Task<AssignmentSeriesResponse> CreateAssignmentSeriesAsync(
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.CreateAssignmentSeriesAsync(request, cancellationToken);

    public Task<AssignmentSeriesResponse?> UpdateAssignmentSeriesAsync(
        int id,
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    ) => seriesService.UpdateAssignmentSeriesAsync(id, request, cancellationToken);

    public Task<AssignmentSeriesResponse?> PublishAssignmentSeriesAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => seriesService.PublishAssignmentSeriesAsync(id, cancellationToken);

    public Task<AssignmentSeriesResponse?> ExpireAssignmentSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    ) => seriesService.ExpireAssignmentSeriesAsync(id, request, cancelledByUserId, cancellationToken);

    public Task<bool> DeleteAssignmentSeriesAsync(int id, CancellationToken cancellationToken = default) =>
        seriesService.DeleteAssignmentSeriesAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentEntryResponse>> GetAssignmentEntriesAsync(
        AssignmentEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    ) => entryService.GetAssignmentEntriesAsync(queryParams, cancellationToken);

    public Task<AssignmentEntryResponse?> GetAssignmentEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => entryService.GetAssignmentEntryByIdAsync(id, cancellationToken);

    public Task<AssignmentEntryResponse> CreateAssignmentEntryAsync(
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    ) => entryService.CreateAssignmentEntryAsync(request, cancellationToken);

    public Task<AssignmentEntryResponse?> UpdateAssignmentEntryAsync(
        int id,
        AssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    ) => entryService.UpdateAssignmentEntryAsync(id, request, cancellationToken, conflictOverrideActorId);

    public Task<AssignmentEntryResponse?> PublishAssignmentEntryAsync(
        int id,
        CancellationToken cancellationToken = default
    ) => entryService.PublishAssignmentEntryAsync(id, cancellationToken);

    public Task<AssignmentEntryResponse?> ExpireAssignmentEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    ) => entryService.ExpireAssignmentEntryAsync(id, request, cancelledByUserId, cancellationToken);

    public Task<bool> DeleteAssignmentEntryAsync(int id, CancellationToken cancellationToken = default) =>
        entryService.DeleteAssignmentEntryAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<AssignmentEntry>> PublishEntriesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default
    ) => ((IAssignmentPublicationService)entryService).PublishEntriesAsync(ids, cancellationToken);
}
