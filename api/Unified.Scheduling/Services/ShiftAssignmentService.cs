using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftAssignmentService(ILogger<ShiftAssignmentService> logger, UnifiedDbContext db)
    : IShiftAssignmentService
{
    public async Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateOrUpdateShiftEntryLinkAsync(
            request,
            updateExisting: false,
            expectedLinkId: null,
            cancellationToken
        );
    }

    public async Task<ShiftAssignmentEntryResponse?> UpdateShiftEntryLinkAsync(
        int id,
        ShiftAssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var linkIdentity = await db
            .ShiftAssignmentEntries.AsNoTracking()
            .Where(link => link.Id == id)
            .Select(link => new { link.ShiftEntryId, link.AssignmentEntryId })
            .SingleOrDefaultAsync(cancellationToken);
        if (linkIdentity is null)
            return null;

        return await CreateOrUpdateShiftEntryLinkAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = linkIdentity.ShiftEntryId,
                AssignmentEntryId = linkIdentity.AssignmentEntryId,
                UserIds = request.UserIds,
            },
            updateExisting: true,
            expectedLinkId: id,
            cancellationToken
        );
    }

    public async Task<bool> DeleteShiftEntryLinkAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var link = await db
            .ShiftAssignmentEntries.Include(entryLink => entryLink.Users)
            .SingleOrDefaultAsync(entryLink => entryLink.Id == id, cancellationToken);
        if (link is null)
            return false;

        if (link.ShiftAssignmentSeriesLinkId.HasValue)
            SuppressSeriesBackedLinks([link]);
        else
            RemoveLinks([link]);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Unlinked shift-assignment entry link {ShiftAssignmentEntryLinkId}.", id);
        return true;
    }

    public async Task<ShiftAssignmentSeriesLinkResponse> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateOrUpdateShiftSeriesLinkAsync(
            request,
            updateExisting: false,
            expectedLinkId: null,
            cancellationToken
        );
    }

    public async Task<ShiftAssignmentSeriesLinkResponse?> UpdateShiftSeriesLinkAsync(
        int id,
        ShiftAssignmentSeriesUpdateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var linkIdentity = await db
            .ShiftAssignmentSeriesLinks.AsNoTracking()
            .Where(link => link.Id == id)
            .Select(link => new { link.ShiftSeriesId, link.AssignmentSeriesId })
            .SingleOrDefaultAsync(cancellationToken);
        if (linkIdentity is null)
            return null;

        return await CreateOrUpdateShiftSeriesLinkAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = linkIdentity.ShiftSeriesId,
                AssignmentSeriesId = linkIdentity.AssignmentSeriesId,
                AssignedUserIds = request.AssignedUserIds,
            },
            updateExisting: true,
            expectedLinkId: id,
            cancellationToken
        );
    }

    public async Task<bool> DeleteShiftSeriesLinkAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var link = await db
            .ShiftAssignmentSeriesLinks.Include(seriesLink => seriesLink.Users)
            .Include(seriesLink => seriesLink.EntryLinks)
                .ThenInclude(entryLink => entryLink.Users)
            .SingleOrDefaultAsync(seriesLink => seriesLink.Id == id, cancellationToken);
        if (link is null)
            return false;

        RemoveSeriesLinks([link]);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Removed shift-assignment series link {ShiftAssignmentSeriesLinkId}.", id);
        return true;
    }

    private async Task<ShiftAssignmentSeriesLinkResponse> CreateOrUpdateShiftSeriesLinkAsync(
        ShiftAssignmentSeriesRequest request,
        bool updateExisting,
        int? expectedLinkId,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var selectedUserIds = ValidateSelectedUserIds(request.AssignedUserIds);
        var shiftSeriesExists = await db.ShiftSeries.AnyAsync(
            series => series.Id == request.ShiftSeriesId,
            cancellationToken
        );
        var assignmentSeriesExists = await db.AssignmentSeries.AnyAsync(
            series => series.Id == request.AssignmentSeriesId,
            cancellationToken
        );

        if (!shiftSeriesExists)
            throw new KeyNotFoundException($"Shift series {request.ShiftSeriesId} not found.");
        if (!assignmentSeriesExists)
            throw new KeyNotFoundException($"Assignment series {request.AssignmentSeriesId} not found.");

        var shiftEntries = await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == request.ShiftSeriesId)
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);
        var assignmentEntries = await db
            .AssignmentEntries.Include(entry => entry.Event)
            .Where(entry => entry.AssignmentSeriesId == request.AssignmentSeriesId)
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .ToListAsync(cancellationToken);

        var intersections = (
            from shiftEntry in shiftEntries
            from assignmentEntry in assignmentEntries
            where UtcIntervalsOverlap(shiftEntry.Event!, assignmentEntry.Event!)
            select (shiftEntry, assignmentEntry)
        ).ToList();

        if (intersections.Count == 0)
            throw new InvalidOperationException(
                $"Shift series {request.ShiftSeriesId} did not overlap any assignment entries in assignment series {request.AssignmentSeriesId}."
            );

        foreach (var (shiftEntry, _) in intersections)
        {
            var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
            if (!selectedUserIds.All(shiftUserIds.Contains))
            {
                logger.LogInformation(
                    "Invalid selected users for shift entry {ShiftEntryId} during series assignment link.",
                    shiftEntry.Id
                );
                throw new InvalidOperationException("Selected users must belong to every intersecting shift entry.");
            }
        }

        var shiftEntryIds = intersections.Select(pair => pair.shiftEntry.Id).Distinct().ToList();
        var assignmentEntryIds = intersections.Select(pair => pair.assignmentEntry.Id).Distinct().ToList();
        var existingLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.ShiftAssignmentSeriesLink)
                .ThenInclude(link => link!.Users)
            .Where(link =>
                shiftEntryIds.Contains(link.ShiftEntryId) && assignmentEntryIds.Contains(link.AssignmentEntryId)
            )
            .ToListAsync(cancellationToken);

        var seriesLink = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(link => link.Users)
            .SingleOrDefaultAsync(
                link =>
                    link.ShiftSeriesId == request.ShiftSeriesId
                    && link.AssignmentSeriesId == request.AssignmentSeriesId,
                cancellationToken
            );

        if (seriesLink is null)
        {
            if (updateExisting)
                throw new KeyNotFoundException($"Shift-assignment series link {expectedLinkId} not found.");

            var conflictingLink = existingLinks.FirstOrDefault(link => link.ShiftAssignmentSeriesLinkId is not null);
            if (conflictingLink is not null)
                throw new InvalidOperationException(
                    $"Cannot link shift series {request.ShiftSeriesId} to assignment series {request.AssignmentSeriesId} because shift entry {conflictingLink.ShiftEntryId} and assignment entry {conflictingLink.AssignmentEntryId} are already linked by another series link."
                );

            var manualLink = existingLinks.FirstOrDefault(link => link.ShiftAssignmentSeriesLinkId is null);
            if (manualLink is not null)
                throw new InvalidOperationException(
                    $"Cannot link shift series {request.ShiftSeriesId} to assignment series {request.AssignmentSeriesId} because shift entry {manualLink.ShiftEntryId} is already manually linked to assignment entry {manualLink.AssignmentEntryId}."
                );

            seriesLink = new ShiftAssignmentSeriesLink
            {
                ShiftSeriesId = request.ShiftSeriesId,
                AssignmentSeriesId = request.AssignmentSeriesId,
            };
            db.ShiftAssignmentSeriesLinks.Add(seriesLink);
        }
        else
        {
            if (!updateExisting)
                throw new InvalidOperationException("Shift series is already linked to this assignment series.");
            if (expectedLinkId.HasValue && seriesLink.Id != expectedLinkId.Value)
                throw new KeyNotFoundException($"Shift-assignment series link {expectedLinkId} not found.");

            var conflictingLink = existingLinks.FirstOrDefault(link =>
                link.ShiftAssignmentSeriesLinkId is not null && link.ShiftAssignmentSeriesLinkId != seriesLink.Id
            );
            if (conflictingLink is not null)
                throw new InvalidOperationException(
                    $"Cannot link shift series {request.ShiftSeriesId} to assignment series {request.AssignmentSeriesId} because shift entry {conflictingLink.ShiftEntryId} and assignment entry {conflictingLink.AssignmentEntryId} are already linked by another series link."
                );

            var manualLink = existingLinks.FirstOrDefault(link => link.ShiftAssignmentSeriesLinkId is null);
            if (manualLink is not null)
                throw new InvalidOperationException(
                    $"Cannot link shift series {request.ShiftSeriesId} to assignment series {request.AssignmentSeriesId} because shift entry {manualLink.ShiftEntryId} is already manually linked to assignment entry {manualLink.AssignmentEntryId}."
                );
        }

        SyncSeriesLinkUsers(seriesLink, selectedUserIds);

        var intersectionKeys = intersections.Select(pair => (pair.shiftEntry.Id, pair.assignmentEntry.Id)).ToHashSet();
        var existingLinksByPair = existingLinks.ToDictionary(
            link => (link.ShiftEntryId, link.AssignmentEntryId),
            link => link
        );
        var syncedLinks = new List<ShiftAssignmentEntry>();
        foreach (var (shiftEntry, assignmentEntry) in intersections)
        {
            if (existingLinksByPair.TryGetValue((shiftEntry.Id, assignmentEntry.Id), out var existingLink))
            {
                if (existingLink.ShiftAssignmentSeriesLinkId == seriesLink.Id)
                {
                    if (!existingLink.IsException)
                        SyncLinkUsers(existingLink, selectedUserIds);
                }
                syncedLinks.Add(existingLink);
                continue;
            }

            var link = CreateLink(shiftEntry.Id, assignmentEntry.Id, selectedUserIds, seriesLink, isException: false);
            db.ShiftAssignmentEntries.Add(link);
            syncedLinks.Add(link);
        }

        var obsoleteGeneratedLinks = seriesLink
            .EntryLinks.Where(link => !intersectionKeys.Contains((link.ShiftEntryId, link.AssignmentEntryId)))
            .Where(link => !link.IsException)
            .ToList();
        RemoveLinks(obsoleteGeneratedLinks);
        var responseLinks = seriesLink
            .EntryLinks.Where(link => !obsoleteGeneratedLinks.Contains(link))
            .Concat(syncedLinks)
            .DistinctBy(link => (link.ShiftEntryId, link.AssignmentEntryId))
            .ToList();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Linked shift series {ShiftSeriesId} to assignment series {AssignmentSeriesId}; created {LinkCount} links.",
            request.ShiftSeriesId,
            request.AssignmentSeriesId,
            syncedLinks.Count
        );

        return MapToSeriesLinkResponse(seriesLink, responseLinks, assignmentEntries);
    }

    private async Task<ShiftAssignmentEntryResponse> CreateOrUpdateShiftEntryLinkAsync(
        ShiftAssignmentEntryRequest request,
        bool updateExisting,
        int? expectedLinkId,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var response = await UpsertSingleShiftAssignmentLinkAsync(
            request.ShiftEntryId,
            request.AssignmentEntryId,
            request.UserIds,
            cancellationToken,
            updateExisting,
            expectedLinkId
        );
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task<ShiftAssignmentEntryResponse> UpsertSingleShiftAssignmentLinkAsync(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> assignedUserIds,
        CancellationToken cancellationToken,
        bool updateExisting = true,
        int? expectedLinkId = null
    )
    {
        var selectedUserIds = ValidateSelectedUserIds(assignedUserIds);
        var shiftEntry = await LoadShiftEntryAsync(shiftEntryId, cancellationToken);
        var assignmentEntry = await LoadAssignmentEntryAsync(assignmentEntryId, cancellationToken);

        ValidateCanLink(shiftEntry, assignmentEntry, selectedUserIds);

        var link = await db
            .ShiftAssignmentEntries.Include(existingLink => existingLink.Users)
            .Include(existingLink => existingLink.ShiftAssignmentSeriesLink)
                .ThenInclude(seriesLink => seriesLink!.Users)
            .SingleOrDefaultAsync(
                existingLink =>
                    existingLink.ShiftEntryId == shiftEntry.Id && existingLink.AssignmentEntryId == assignmentEntry.Id,
                cancellationToken
            );

        if (expectedLinkId.HasValue && link?.Id != expectedLinkId.Value)
            throw new KeyNotFoundException($"Shift-assignment entry link {expectedLinkId.Value} not found.");

        if (link is not null)
        {
            if (!updateExisting)
                throw new InvalidOperationException("Shift entry is already linked to this assignment entry.");

            SyncLinkUsers(link, selectedUserIds);
            UpdateExceptionState(link, selectedUserIds);
        }
        else
        {
            link = CreateLink(shiftEntry.Id, assignmentEntry.Id, selectedUserIds);
            db.ShiftAssignmentEntries.Add(link);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Linked shift entry {ShiftEntryId} to assignment entry {AssignmentEntryId}.",
            shiftEntry.Id,
            assignmentEntry.Id
        );

        return MapToResponse(link, assignmentEntry.Capacity);
    }

    private async Task<ShiftEntry> LoadShiftEntryAsync(int id, CancellationToken cancellationToken) =>
        await db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Shift entry {id} not found.");

    private async Task<AssignmentEntry> LoadAssignmentEntryAsync(int id, CancellationToken cancellationToken) =>
        await db
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Assignment entry {id} not found.");

    private void ValidateCanLink(
        ShiftEntry shiftEntry,
        AssignmentEntry assignmentEntry,
        IReadOnlyCollection<Guid> selectedUserIds
    )
    {
        if (shiftEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
        {
            logger.LogInformation("Blocked link to cancelled shift entry {ShiftEntryId}.", shiftEntry.Id);
            throw new InvalidOperationException("Cancelled shift entries cannot be linked.");
        }

        if (assignmentEntry.Event?.StatusTypeCode == CalendarEventStatusTypeCodes.Cancelled)
        {
            logger.LogInformation(
                "Blocked link to cancelled assignment entry {AssignmentEntryId}.",
                assignmentEntry.Id
            );
            throw new InvalidOperationException("Cancelled assignment entries cannot be linked.");
        }

        if (
            shiftEntry.Event is null
            || assignmentEntry.Event is null
            || !UtcIntervalsOverlap(shiftEntry.Event, assignmentEntry.Event)
        )
            throw new InvalidOperationException("Shift and assignment entries must overlap.");

        var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        if (!selectedUserIds.All(shiftUserIds.Contains))
        {
            logger.LogInformation("Invalid selected users for shift entry {ShiftEntryId}.", shiftEntry.Id);
            throw new InvalidOperationException("Selected users must belong to the linked shift entry.");
        }
    }

    private static bool UtcIntervalsOverlap(Event shiftEvent, Event assignmentEvent) =>
        ShiftAssignmentGuards.UtcIntervalsOverlap(
            shiftEvent.StartAtUtc,
            shiftEvent.EndAtUtc,
            assignmentEvent.StartAtUtc,
            assignmentEvent.EndAtUtc
        );

    private static IReadOnlyCollection<Guid> ValidateSelectedUserIds(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
            throw new InvalidOperationException("At least one selected user is required.");

        var distinctUserIds = userIds.Distinct().ToList();
        if (distinctUserIds.Count != userIds.Count)
            throw new InvalidOperationException("Selected users must be unique.");

        return distinctUserIds;
    }

    private static ShiftAssignmentEntry CreateLink(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> selectedUserIds,
        ShiftAssignmentSeriesLink? seriesLink = null,
        bool isException = false
    ) =>
        new()
        {
            ShiftEntryId = shiftEntryId,
            AssignmentEntryId = assignmentEntryId,
            ShiftAssignmentSeriesLink = seriesLink,
            IsException = isException,
            Users = selectedUserIds.Select(userId => new ShiftAssignmentEntryUser { UserId = userId }).ToList(),
        };

    private void SyncSeriesLinkUsers(ShiftAssignmentSeriesLink link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(link.Users);
        link.Users.Clear();
        foreach (var userId in selectedUserIds)
            link.Users.Add(new ShiftAssignmentSeriesLinkUser { UserId = userId });
    }

    private void RemoveSeriesLinks(IReadOnlyCollection<ShiftAssignmentSeriesLink> seriesLinks)
    {
        if (seriesLinks.Count == 0)
            return;

        var entryLinks = seriesLinks.SelectMany(link => link.EntryLinks).ToList();
        RemoveLinks(entryLinks);
        db.ShiftAssignmentSeriesLinkUsers.RemoveRange(seriesLinks.SelectMany(link => link.Users));
        db.ShiftAssignmentSeriesLinks.RemoveRange(seriesLinks);
    }

    private void SyncLinkUsers(ShiftAssignmentEntry link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        db.ShiftAssignmentEntryUsers.RemoveRange(link.Users);
        link.Users.Clear();
        foreach (var userId in selectedUserIds)
            link.Users.Add(new ShiftAssignmentEntryUser { ShiftAssignmentEntryId = link.Id, UserId = userId });
    }

    private static void UpdateExceptionState(ShiftAssignmentEntry link, IReadOnlyCollection<Guid> selectedUserIds)
    {
        if (link.ShiftAssignmentSeriesLink is null)
        {
            link.IsException = false;
            return;
        }

        var parentUserIds = link.ShiftAssignmentSeriesLink.Users.Select(user => user.UserId).ToHashSet();
        link.IsException = !selectedUserIds.ToHashSet().SetEquals(parentUserIds);
    }

    private void RemoveLinks(IReadOnlyCollection<ShiftAssignmentEntry> links)
    {
        if (links.Count == 0)
            return;

        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
    }

    private void SuppressSeriesBackedLinks(IReadOnlyCollection<ShiftAssignmentEntry> links)
    {
        if (links.Count == 0)
            return;

        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        foreach (var link in links)
            link.IsException = true;
    }

    private static ShiftAssignmentEntryResponse MapToResponse(ShiftAssignmentEntry link, int capacity)
    {
        var userIds = link.Users.Select(user => user.UserId).Distinct().ToList();
        return new ShiftAssignmentEntryResponse
        {
            Id = link.Id,
            ShiftEntryId = link.ShiftEntryId,
            AssignmentEntryId = link.AssignmentEntryId,
            ShiftAssignmentSeriesLinkId = link.ShiftAssignmentSeriesLinkId,
            IsException = link.IsException,
            Capacity = capacity,
            AssignedUserCount = userIds.Count,
            UserIds = userIds,
        };
    }

    private static ShiftAssignmentSeriesLinkResponse MapToSeriesLinkResponse(
        ShiftAssignmentSeriesLink seriesLink,
        IReadOnlyCollection<ShiftAssignmentEntry> entryLinks,
        IReadOnlyCollection<AssignmentEntry> assignmentEntries
    )
    {
        var assignmentCapacityById = assignmentEntries.ToDictionary(entry => entry.Id, entry => entry.Capacity);
        var entryLinkResponses = entryLinks
            .Select(link => MapToResponse(link, assignmentCapacityById.GetValueOrDefault(link.AssignmentEntryId)))
            .ToList();

        return new ShiftAssignmentSeriesLinkResponse
        {
            Id = seriesLink.Id,
            ShiftSeriesId = seriesLink.ShiftSeriesId,
            AssignmentSeriesId = seriesLink.AssignmentSeriesId,
            AssignedUserIds = seriesLink.Users.Select(user => user.UserId).Distinct().ToList(),
            ShiftAssignmentEntryIds = entryLinkResponses.Select(link => link.Id).ToList(),
            EntryLinks = entryLinkResponses,
            ExceptionCount = entryLinkResponses.Count(link => link.IsException),
        };
    }
}
