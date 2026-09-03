using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Models;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictService(
    IEnumerable<ICalendarConflictParticipantProvider> participantProviders,
    UnifiedDbContext db,
    TimeProvider? timeProvider = null
) : ICalendarConflictService
{
    private readonly IReadOnlyCollection<ICalendarConflictParticipantProvider> _participantProviders =
        participantProviders.ToList();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public IReadOnlyCollection<CalendarConflict> DetectConflicts(
        IReadOnlyCollection<CalendarConflictParticipant> participants
    ) => CalendarConflictDetector.Detect(participants);

    public async Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var participants = await LoadParticipantsAsync(query, cancellationToken);
        return await ApplyOverridesAsync(CalendarConflictDetector.Detect(participants), cancellationToken);
    }

    public async Task EnsureNoUnresolvedConflictsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CancellationToken cancellationToken = default
    )
    {
        if (candidates.Count == 0)
            return;

        var candidateEventIds = candidates
            .Where(candidate => candidate.EventId.HasValue)
            .Select(candidate => candidate.EventId!.Value)
            .ToHashSet();
        var query = new CalendarConflictQuery(
            candidates.Min(candidate => candidate.Start),
            candidates.Max(candidate => candidate.End),
            candidates.Select(candidate => candidate.ResourceId).Distinct().ToList(),
            candidateEventIds.ToList(),
            IncludeDraftParticipants: false
        );
        var existing = await LoadParticipantsAsync(query, cancellationToken);
        var allParticipants = candidates.Concat(existing).ToList();
        var candidateSet = candidates.ToHashSet();
        var conflicts = CalendarConflictDetector
            .Detect(allParticipants)
            .Where(conflict => candidateSet.Contains(conflict.Entry) || candidateSet.Contains(conflict.Overlaps))
            .ToList();

        var unresolved = (await ApplyOverridesAsync(conflicts, cancellationToken))
            .Where(conflict => !conflict.IsOverridden)
            .ToList();
        if (unresolved.Count > 0)
            throw new CalendarConflictException(unresolved);
    }

    public async Task<CalendarConflictOverrideResponse> CreateOverrideAsync(
        CalendarConflictOverrideRequest request,
        Guid? createdById,
        CancellationToken cancellationToken = default
    )
    {
        if (request.FirstEventId == request.SecondEventId)
            throw new InvalidOperationException("A conflict override requires two different calendar events.");
        if (request.ResourceId == Guid.Empty)
            throw new InvalidOperationException("A conflict override requires a resource.");
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new InvalidOperationException("A conflict override note is required.");

        var key = CalendarConflictKey.Create(request.FirstEventId, request.SecondEventId, request.ResourceId);
        var events = await db
            .Events.AsNoTracking()
            .Where(eventEntity => eventEntity.Id == key.FirstEventId || eventEntity.Id == key.SecondEventId)
            .ToListAsync(cancellationToken);
        if (events.Count != 2)
            throw new KeyNotFoundException("Both calendar events must exist before a conflict can be overridden.");

        var rangeStart = events.Min(eventEntity => eventEntity.StartAtUtc);
        var rangeEnd = events.Max(eventEntity => eventEntity.EndAtUtc ?? eventEntity.StartAtUtc.AddTicks(1));
        var participants = await LoadParticipantsAsync(
            new CalendarConflictQuery(rangeStart, rangeEnd),
            cancellationToken
        );
        var conflict = CalendarConflictDetector
            .Detect(participants)
            .FirstOrDefault(candidate => CalendarConflictKey.Create(candidate) == key);
        if (conflict is null)
            throw new InvalidOperationException("The selected events do not currently constitute an active conflict.");

        var now = _timeProvider.GetUtcNow();
        var overrideEntity = await db.CalendarConflictOverrides.SingleOrDefaultAsync(
            candidate =>
                candidate.FirstEventId == key.FirstEventId
                && candidate.SecondEventId == key.SecondEventId
                && candidate.ResourceId == key.ResourceId,
            cancellationToken
        );
        if (overrideEntity is null)
        {
            overrideEntity = new CalendarConflictOverride
            {
                FirstEventId = key.FirstEventId,
                SecondEventId = key.SecondEventId,
                ResourceId = key.ResourceId,
                Note = request.Note.Trim(),
                IsActive = true,
                CreatedById = createdById,
                CreatedOn = now,
            };
            db.CalendarConflictOverrides.Add(overrideEntity);
        }
        else
        {
            overrideEntity.Note = request.Note.Trim();
            overrideEntity.IsActive = true;
            overrideEntity.InvalidatedOn = null;
            overrideEntity.UpdatedById = createdById;
            overrideEntity.UpdatedOn = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CalendarConflictOverrideResponse(
            overrideEntity.Id,
            overrideEntity.FirstEventId,
            overrideEntity.SecondEventId,
            overrideEntity.ResourceId,
            overrideEntity.Note,
            overrideEntity.CreatedById,
            overrideEntity.CreatedOn,
            overrideEntity.UpdatedById,
            overrideEntity.UpdatedOn
        );
    }

    public async Task InvalidateResolvedOverridesAsync(
        IReadOnlyCollection<int> eventIds,
        Guid? updatedById = null,
        CancellationToken cancellationToken = default
    )
    {
        if (eventIds.Count == 0)
            return;

        var ids = eventIds.Distinct().ToList();
        var overrides = await db
            .CalendarConflictOverrides.Where(overrideEntity =>
                overrideEntity.IsActive
                && (ids.Contains(overrideEntity.FirstEventId) || ids.Contains(overrideEntity.SecondEventId))
            )
            .ToListAsync(cancellationToken);
        if (overrides.Count == 0)
            return;

        var overrideEventIds = overrides
            .SelectMany(overrideEntity => new[] { overrideEntity.FirstEventId, overrideEntity.SecondEventId })
            .Distinct()
            .ToList();
        var events = await db
            .Events.AsNoTracking()
            .Where(eventEntity => overrideEventIds.Contains(eventEntity.Id))
            .ToListAsync(cancellationToken);
        var activeConflictKeys = new HashSet<CalendarConflictKey>();
        if (events.Count > 0)
        {
            var rangeStart = events.Min(eventEntity => eventEntity.StartAtUtc);
            var rangeEnd = events.Max(eventEntity => eventEntity.EndAtUtc ?? eventEntity.StartAtUtc.AddTicks(1));
            var participants = await LoadParticipantsAsync(
                new CalendarConflictQuery(rangeStart, rangeEnd),
                cancellationToken
            );
            activeConflictKeys = CalendarConflictDetector
                .Detect(participants)
                .Select(CalendarConflictKey.Create)
                .OfType<CalendarConflictKey>()
                .ToHashSet();
        }

        var resolvedOverrides = overrides
            .Where(overrideEntity =>
                !activeConflictKeys.Contains(
                    CalendarConflictKey.Create(
                        overrideEntity.FirstEventId,
                        overrideEntity.SecondEventId,
                        overrideEntity.ResourceId
                    )
                )
            )
            .ToList();
        if (resolvedOverrides.Count == 0)
            return;

        var now = _timeProvider.GetUtcNow();
        foreach (var overrideEntity in resolvedOverrides)
        {
            overrideEntity.IsActive = false;
            overrideEntity.InvalidatedOn = now;
            overrideEntity.UpdatedOn = now;
            overrideEntity.UpdatedById = updatedById;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CalendarConflictParticipant>> LoadParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken
    )
    {
        var providerResults = await Task.WhenAll(
            _participantProviders.Select(provider => provider.GetParticipantsAsync(query, cancellationToken))
        );
        return providerResults.SelectMany(participants => participants).ToList();
    }

    private async Task<IReadOnlyCollection<CalendarConflict>> ApplyOverridesAsync(
        IReadOnlyCollection<CalendarConflict> conflicts,
        CancellationToken cancellationToken
    )
    {
        var persistedConflicts = conflicts
            .Where(conflict => conflict.Entry.EventId.HasValue && conflict.Overlaps.EventId.HasValue)
            .ToList();
        if (persistedConflicts.Count == 0)
            return conflicts;

        var eventIds = persistedConflicts
            .SelectMany(conflict => new[] { conflict.Entry.EventId!.Value, conflict.Overlaps.EventId!.Value })
            .Distinct()
            .ToList();
        var overrides = await db
            .CalendarConflictOverrides.AsNoTracking()
            .Where(overrideEntity =>
                overrideEntity.IsActive
                && eventIds.Contains(overrideEntity.FirstEventId)
                && eventIds.Contains(overrideEntity.SecondEventId)
            )
            .ToListAsync(cancellationToken);
        var overridesByConflict = overrides.ToDictionary(overrideEntity =>
            CalendarConflictKey.Create(
                overrideEntity.FirstEventId,
                overrideEntity.SecondEventId,
                overrideEntity.ResourceId
            )
        );

        return conflicts.Select(conflict => ApplyOverride(conflict, overridesByConflict)).ToList();
    }

    private static CalendarConflict ApplyOverride(
        CalendarConflict conflict,
        IReadOnlyDictionary<CalendarConflictKey, CalendarConflictOverride> overridesByConflict
    )
    {
        if (CalendarConflictKey.Create(conflict) is not { } key)
            return conflict;

        if (!overridesByConflict.TryGetValue(key, out var overrideEntity))
            return conflict;

        return conflict with
        {
            IsOverridden = true,
            OverrideId = overrideEntity.Id,
            OverrideNote = overrideEntity.Note,
            CreatedById = overrideEntity.CreatedById,
            CreatedOn = overrideEntity.CreatedOn,
            UpdatedById = overrideEntity.UpdatedById,
            UpdatedOn = overrideEntity.UpdatedOn,
        };
    }
}
