using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftAssignmentService(
    ILogger<ShiftAssignmentService> logger,
    UnifiedDbContext db,
    ICalendarDateTimeService calendarDateTimeService
) : IShiftAssignmentService
{
    public async Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateOrUpdateShiftEntryLinkAsync(request, updateExisting: false, cancellationToken);
    }

    public async Task<ShiftAssignmentEntryResponse> UpsertShiftEntryLinkAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return await CreateOrUpdateShiftEntryLinkAsync(request, updateExisting: true, cancellationToken);
    }

    public async Task<ShiftAssignmentSeriesLinkResponse> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    )
    {
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
            where LocalIntervalsOverlap(shiftEntry.Event!, assignmentEntry.Event!)
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
                shiftEntryIds.Contains(link.ShiftEntryId)
                && assignmentEntryIds.Contains(link.AssignmentEntryId)
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
            var conflictingLink = existingLinks.FirstOrDefault(link =>
                link.ShiftAssignmentSeriesLinkId is not null
                && link.ShiftAssignmentSeriesLinkId != seriesLink.Id
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

        var intersectionKeys = intersections
            .Select(pair => (pair.shiftEntry.Id, pair.assignmentEntry.Id))
            .ToHashSet();
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

            var link = CreateLink(
                shiftEntry.Id,
                assignmentEntry.Id,
                selectedUserIds,
                seriesLink,
                isException: false
            );
            db.ShiftAssignmentEntries.Add(link);
            syncedLinks.Add(link);
        }

        var obsoleteGeneratedLinks = seriesLink.EntryLinks
            .Where(link => !intersectionKeys.Contains((link.ShiftEntryId, link.AssignmentEntryId)))
            .Where(link => !link.IsException)
            .ToList();
        RemoveLinks(obsoleteGeneratedLinks);
        var responseLinks = seriesLink
            .EntryLinks.Where(link => !obsoleteGeneratedLinks.Contains(link))
            .Concat(syncedLinks)
            .DistinctBy(link => (link.ShiftEntryId, link.AssignmentEntryId))
            .ToList();

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Linked shift series {ShiftSeriesId} to assignment series {AssignmentSeriesId}; created {LinkCount} links.",
            request.ShiftSeriesId,
            request.AssignmentSeriesId,
            syncedLinks.Count
        );

        return MapToSeriesLinkResponse(seriesLink, responseLinks, assignmentEntries);
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertShiftEntryLinksAsync(
        int shiftEntryId,
        IReadOnlyCollection<int>? assignmentEntryIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentEntryIds is null && assignedUserIds is null)
            return [];

        if (assignmentEntryIds is null)
            throw new InvalidOperationException("AssignmentEntryIds must be provided when AssignedUserIds is provided.");

        var requestedAssignmentEntryIds = assignmentEntryIds.Distinct().ToHashSet();
        if (requestedAssignmentEntryIds.Count > 0 && assignedUserIds is null)
            throw new InvalidOperationException("AssignedUserIds must be provided when AssignmentEntryIds are provided.");
        var requestedUserIds = assignedUserIds ?? [];

        var existingLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.AssignmentEntry)
                .ThenInclude(entry => entry!.Event)
            .Where(link => link.ShiftEntryId == shiftEntryId)
            .ToListAsync(cancellationToken);
        var linksToRemove = existingLinks
            .Where(link => link.ShiftAssignmentSeriesLinkId is null)
            .Where(link => !requestedAssignmentEntryIds.Contains(link.AssignmentEntryId))
            .ToList();
        RemoveLinks(linksToRemove);

        if (requestedAssignmentEntryIds.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return [];
        }

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var assignmentEntryId in requestedAssignmentEntryIds)
        {
            links.Add(
                await UpsertSingleShiftAssignmentLinkAsync(
                    shiftEntryId,
                    assignmentEntryId,
                    requestedUserIds,
                    cancellationToken
                )
            );
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertShiftEntryLinksAsync(
        int shiftEntryId,
        IReadOnlyCollection<AssignmentEntryLinkRequest>? assignmentEntryLinks,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentEntryLinks is null)
            return [];

        var requestedAssignmentEntryIds = assignmentEntryLinks.Select(link => link.AssignmentEntryId).Distinct().ToHashSet();
        var existingLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.AssignmentEntry)
                .ThenInclude(entry => entry!.Event)
            .Where(link => link.ShiftEntryId == shiftEntryId)
            .ToListAsync(cancellationToken);
        var linksToRemove = existingLinks
            .Where(link => link.ShiftAssignmentSeriesLinkId is null)
            .Where(link => !requestedAssignmentEntryIds.Contains(link.AssignmentEntryId))
            .ToList();
        RemoveLinks(linksToRemove);

        if (requestedAssignmentEntryIds.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return [];
        }

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var link in assignmentEntryLinks)
        {
            links.Add(
                await UpsertSingleShiftAssignmentLinkAsync(
                    shiftEntryId,
                    link.AssignmentEntryId,
                    link.AssignedUserIds,
                    cancellationToken
                )
            );
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        int shiftSeriesId,
        IReadOnlyCollection<int>? assignmentSeriesIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentSeriesIds is null)
            return [];

        var requestedAssignmentSeriesIds = assignmentSeriesIds.Distinct().ToHashSet();
        await RemoveShiftSeriesLinksNotRequestedAsync(
            shiftSeriesId,
            requestedAssignmentSeriesIds,
            cancellationToken
        );

        if (requestedAssignmentSeriesIds.Count == 0)
            return [];

        if (assignedUserIds is null)
            throw new InvalidOperationException("AssignedUserIds must be provided when AssignmentSeriesIds are provided.");

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var assignmentSeriesId in requestedAssignmentSeriesIds)
        {
            var seriesLink = await LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeriesId,
                    AssignmentSeriesId = assignmentSeriesId,
                    AssignedUserIds = assignedUserIds,
                },
                cancellationToken
            );
            links.AddRange(seriesLink.EntryLinks);
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        int shiftSeriesId,
        IReadOnlyCollection<AssignmentSeriesLinkRequest>? assignmentSeriesLinks,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentSeriesLinks is null)
            return [];

        var requestedAssignmentSeriesIds = assignmentSeriesLinks.Select(link => link.AssignmentSeriesId).ToHashSet();
        await RemoveShiftSeriesLinksNotRequestedAsync(
            shiftSeriesId,
            requestedAssignmentSeriesIds,
            cancellationToken
        );

        if (requestedAssignmentSeriesIds.Count == 0)
            return [];

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var assignmentSeriesLink in assignmentSeriesLinks)
        {
            var seriesLink = await LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeriesId,
                    AssignmentSeriesId = assignmentSeriesLink.AssignmentSeriesId,
                    AssignedUserIds = assignmentSeriesLink.AssignedUserIds,
                },
                cancellationToken
            );
            links.AddRange(seriesLink.EntryLinks);
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertAssignmentEntryLinksAsync(
        int assignmentEntryId,
        IReadOnlyCollection<int>? shiftEntryIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftEntryIds is null && assignedUserIds is null)
            return [];

        if (shiftEntryIds is null)
            throw new InvalidOperationException("ShiftEntryIds must be provided when AssignedUserIds is provided.");

        var requestedShiftEntryIds = shiftEntryIds.Distinct().ToHashSet();
        if (requestedShiftEntryIds.Count > 0 && assignedUserIds is null)
            throw new InvalidOperationException("AssignedUserIds must be provided when ShiftEntryIds are provided.");
        var requestedUserIds = assignedUserIds ?? [];

        var existingLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == assignmentEntryId)
            .ToListAsync(cancellationToken);
        var linksToRemove = existingLinks
            .Where(link => link.ShiftAssignmentSeriesLinkId is null)
            .Where(link => !requestedShiftEntryIds.Contains(link.ShiftEntryId))
            .ToList();
        RemoveLinks(linksToRemove);
        SuppressSeriesBackedLinks(
            existingLinks
                .Where(link => link.ShiftAssignmentSeriesLinkId is not null)
                .Where(link => !requestedShiftEntryIds.Contains(link.ShiftEntryId))
                .ToList()
        );

        if (requestedShiftEntryIds.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return [];
        }

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var shiftEntryId in requestedShiftEntryIds)
        {
            links.Add(
                await UpsertSingleShiftAssignmentLinkAsync(
                    shiftEntryId,
                    assignmentEntryId,
                    requestedUserIds,
                    cancellationToken
                )
            );
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertAssignmentEntryLinksAsync(
        int assignmentEntryId,
        IReadOnlyCollection<ShiftEntryLinkRequest>? shiftEntryLinks,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftEntryLinks is null)
            return [];

        var requestedShiftEntryIds = shiftEntryLinks.Select(link => link.ShiftEntryId).Distinct().ToHashSet();
        var existingLinks = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => link.AssignmentEntryId == assignmentEntryId)
            .ToListAsync(cancellationToken);
        var linksToRemove = existingLinks
            .Where(link => link.ShiftAssignmentSeriesLinkId is null)
            .Where(link => !requestedShiftEntryIds.Contains(link.ShiftEntryId))
            .ToList();
        RemoveLinks(linksToRemove);
        SuppressSeriesBackedLinks(
            existingLinks
                .Where(link => link.ShiftAssignmentSeriesLinkId is not null)
                .Where(link => !requestedShiftEntryIds.Contains(link.ShiftEntryId))
                .ToList()
        );

        if (requestedShiftEntryIds.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return [];
        }

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var link in shiftEntryLinks)
        {
            links.Add(
                await UpsertSingleShiftAssignmentLinkAsync(
                    link.ShiftEntryId,
                    assignmentEntryId,
                    link.AssignedUserIds,
                    cancellationToken
                )
            );
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkAssignmentSeriesAsync(
        int assignmentSeriesId,
        IReadOnlyCollection<int>? shiftSeriesIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftSeriesIds is null)
            return [];

        var requestedShiftSeriesIds = shiftSeriesIds.Distinct().ToHashSet();
        await RemoveAssignmentSeriesLinksNotRequestedAsync(
            assignmentSeriesId,
            requestedShiftSeriesIds,
            cancellationToken
        );

        if (requestedShiftSeriesIds.Count == 0)
            return [];

        if (assignedUserIds is null)
            throw new InvalidOperationException("AssignedUserIds must be provided when ShiftSeriesIds are provided.");

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var shiftSeriesId in requestedShiftSeriesIds)
        {
            var seriesLink = await LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeriesId,
                    AssignmentSeriesId = assignmentSeriesId,
                    AssignedUserIds = assignedUserIds,
                },
                cancellationToken
            );
            links.AddRange(seriesLink.EntryLinks);
        }

        return links;
    }

    public async Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkAssignmentSeriesAsync(
        int assignmentSeriesId,
        IReadOnlyCollection<ShiftSeriesLinkRequest>? shiftSeriesLinks,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftSeriesLinks is null)
            return [];

        var requestedShiftSeriesIds = shiftSeriesLinks.Select(link => link.ShiftSeriesId).ToHashSet();
        await RemoveAssignmentSeriesLinksNotRequestedAsync(
            assignmentSeriesId,
            requestedShiftSeriesIds,
            cancellationToken
        );

        if (requestedShiftSeriesIds.Count == 0)
            return [];

        var links = new List<ShiftAssignmentEntryResponse>();
        foreach (var shiftSeriesLink in shiftSeriesLinks)
        {
            var seriesLink = await LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeriesLink.ShiftSeriesId,
                    AssignmentSeriesId = assignmentSeriesId,
                    AssignedUserIds = shiftSeriesLink.AssignedUserIds,
                },
                cancellationToken
            );

            if (seriesLink.EntryLinks.Count == 0)
                throw new InvalidOperationException(
                    $"Shift series {shiftSeriesLink.ShiftSeriesId} did not overlap any assignment entries in assignment series {assignmentSeriesId}."
                );

            links.AddRange(seriesLink.EntryLinks);
        }

        return links;
    }

    private async Task<ShiftAssignmentEntryResponse> CreateOrUpdateShiftEntryLinkAsync(
        ShiftAssignmentEntryRequest request,
        bool updateExisting,
        CancellationToken cancellationToken
    )
    {
        return await UpsertSingleShiftAssignmentLinkAsync(
            request.ShiftEntryId,
            request.AssignmentEntryId,
            request.UserIds,
            cancellationToken,
            updateExisting
        );
    }

    private async Task<ShiftAssignmentEntryResponse> UpsertSingleShiftAssignmentLinkAsync(
        int shiftEntryId,
        int assignmentEntryId,
        IReadOnlyCollection<Guid> assignedUserIds,
        CancellationToken cancellationToken,
        bool updateExisting = true
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

        var shiftUserIds = shiftEntry.Users.Select(user => user.UserId).ToHashSet();
        if (!selectedUserIds.All(shiftUserIds.Contains))
        {
            logger.LogInformation("Invalid selected users for shift entry {ShiftEntryId}.", shiftEntry.Id);
            throw new InvalidOperationException("Selected users must belong to the linked shift entry.");
        }
    }

    private bool LocalIntervalsOverlap(Event shiftEvent, Event assignmentEvent)
    {
        var shiftRange = GetLocalDateTimeRange(shiftEvent);
        var assignmentRange = GetLocalDateTimeRange(assignmentEvent);
        return shiftRange.Start < assignmentRange.End && assignmentRange.Start < shiftRange.End;
    }

    private LocalDateTimeRange GetLocalDateTimeRange(Event eventEntity)
    {
        var timeZone = calendarDateTimeService.ResolveTimeZone(eventEntity.TimeZoneId);
        var localStart = calendarDateTimeService.ToLocalTime(eventEntity.StartAtUtc, timeZone);
        if (!eventEntity.EndAtUtc.HasValue)
            return new LocalDateTimeRange(localStart, localStart.AddTicks(1));

        var localEnd = calendarDateTimeService.ToLocalTime(eventEntity.EndAtUtc.Value, timeZone);
        if (localEnd <= localStart)
            localEnd = localStart.AddTicks(1);

        return new LocalDateTimeRange(localStart, localEnd);
    }

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

    private async Task RemoveShiftSeriesLinksNotRequestedAsync(
        int shiftSeriesId,
        IReadOnlySet<int> requestedAssignmentSeriesIds,
        CancellationToken cancellationToken
    )
    {
        var linksToRemove = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(entryLink => entryLink.Users)
            .Where(link =>
                link.ShiftSeriesId == shiftSeriesId
                && !requestedAssignmentSeriesIds.Contains(link.AssignmentSeriesId)
            )
            .ToListAsync(cancellationToken);

        RemoveSeriesLinks(linksToRemove);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveAssignmentSeriesLinksNotRequestedAsync(
        int assignmentSeriesId,
        IReadOnlySet<int> requestedShiftSeriesIds,
        CancellationToken cancellationToken
    )
    {
        var linksToRemove = await db
            .ShiftAssignmentSeriesLinks.Include(link => link.Users)
            .Include(link => link.EntryLinks)
                .ThenInclude(entryLink => entryLink.Users)
            .Where(link =>
                link.AssignmentSeriesId == assignmentSeriesId
                && !requestedShiftSeriesIds.Contains(link.ShiftSeriesId)
            )
            .ToListAsync(cancellationToken);

        RemoveSeriesLinks(linksToRemove);
        await db.SaveChangesAsync(cancellationToken);
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
            .Select(link => MapToResponse(
                link,
                assignmentCapacityById.GetValueOrDefault(link.AssignmentEntryId)
            ))
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

    private readonly record struct LocalDateTimeRange(DateTime Start, DateTime End);
}
