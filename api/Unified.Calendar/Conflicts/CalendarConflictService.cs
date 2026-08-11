using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Models;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictService(
    ICalendarConflictDetector detector,
    IEnumerable<ICalendarConflictParticipantProvider> participantProviders,
    UnifiedDbContext db
) : ICalendarConflictService
{
    private readonly IReadOnlyCollection<ICalendarConflictParticipantProvider> _participantProviders =
        participantProviders.ToList();

    public IReadOnlyCollection<CalendarConflict> DetectConflicts(
        IReadOnlyCollection<CalendarConflictParticipant> participants
    ) => detector.Detect(participants);

    public async Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var participants = await LoadParticipantsAsync(query, cancellationToken);
        return await ApplyOverridesAsync(detector.Detect(participants), cancellationToken);
    }

    public async Task<IReadOnlyCollection<CalendarConflict>> CheckCandidatesAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (candidates.Count == 0)
            return [];

        var candidateEventIds = candidates
            .Where(candidate => candidate.EventId.HasValue)
            .Select(candidate => candidate.EventId!.Value)
            .ToHashSet();
        var excludedEventIds = (query.ExcludedEventIds ?? []).Concat(candidateEventIds).Distinct().ToList();
        var existing = await LoadParticipantsAsync(
            query with
            {
                ExcludedEventIds = excludedEventIds,
            },
            cancellationToken
        );
        var allParticipants = candidates.Concat(existing).ToList();
        var candidateSet = candidates.ToHashSet();
        var conflicts = detector
            .Detect(allParticipants)
            .Where(conflict => candidateSet.Contains(conflict.Entry) || candidateSet.Contains(conflict.Overlaps))
            .ToList();

        return await ApplyOverridesAsync(conflicts, cancellationToken);
    }

    public async Task<CalendarConflictOverrideResponse> CreateOverrideAsync(
        CalendarConflictOverrideRequest request,
        Guid? createdById,
        CancellationToken cancellationToken = default
    )
    {
        if (request.FirstEventId == request.SecondEventId)
            throw new InvalidOperationException("A conflict override requires two different calendar events.");
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new InvalidOperationException("A conflict override note is required.");

        var firstEventId = Math.Min(request.FirstEventId, request.SecondEventId);
        var secondEventId = Math.Max(request.FirstEventId, request.SecondEventId);
        var events = await db
            .Events.AsNoTracking()
            .Where(eventEntity => eventEntity.Id == firstEventId || eventEntity.Id == secondEventId)
            .ToListAsync(cancellationToken);
        if (events.Count != 2)
            throw new KeyNotFoundException("Both calendar events must exist before a conflict can be overridden.");

        var rangeStart = events.Min(eventEntity => eventEntity.StartAtUtc);
        var rangeEnd = events.Max(eventEntity => eventEntity.EndAtUtc ?? eventEntity.StartAtUtc.AddTicks(1));
        var participants = await LoadParticipantsAsync(
            new CalendarConflictQuery(rangeStart, rangeEnd),
            cancellationToken
        );
        var conflict = detector
            .Detect(participants)
            .FirstOrDefault(candidate => IsPair(candidate, firstEventId, secondEventId));
        if (conflict is null)
            throw new InvalidOperationException("The selected events do not currently constitute an active conflict.");

        var now = DateTimeOffset.UtcNow;
        var overrideEntity = await db.CalendarConflictOverrides.SingleOrDefaultAsync(
            candidate => candidate.FirstEventId == firstEventId && candidate.SecondEventId == secondEventId,
            cancellationToken
        );
        if (overrideEntity is null)
        {
            overrideEntity = new CalendarConflictOverride
            {
                FirstEventId = firstEventId,
                SecondEventId = secondEventId,
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
        var activeConflictPairs = new HashSet<(int FirstEventId, int SecondEventId)>();
        if (events.Count > 0)
        {
            var rangeStart = events.Min(eventEntity => eventEntity.StartAtUtc);
            var rangeEnd = events.Max(eventEntity => eventEntity.EndAtUtc ?? eventEntity.StartAtUtc.AddTicks(1));
            var participants = await LoadParticipantsAsync(
                new CalendarConflictQuery(rangeStart, rangeEnd),
                cancellationToken
            );
            activeConflictPairs = detector
                .Detect(participants)
                .Where(conflict => conflict.Entry.EventId.HasValue && conflict.Overlaps.EventId.HasValue)
                .Select(conflict => NormalizePair(conflict.Entry.EventId!.Value, conflict.Overlaps.EventId!.Value))
                .ToHashSet();
        }

        var resolvedOverrides = overrides
            .Where(overrideEntity =>
                !activeConflictPairs.Contains((overrideEntity.FirstEventId, overrideEntity.SecondEventId))
            )
            .ToList();
        if (resolvedOverrides.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
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
        var overridesByPair = overrides.ToDictionary(overrideEntity =>
            (overrideEntity.FirstEventId, overrideEntity.SecondEventId)
        );

        return conflicts.Select(conflict => ApplyOverride(conflict, overridesByPair)).ToList();
    }

    private static CalendarConflict ApplyOverride(
        CalendarConflict conflict,
        IReadOnlyDictionary<(int FirstEventId, int SecondEventId), CalendarConflictOverride> overridesByPair
    )
    {
        if (!conflict.Entry.EventId.HasValue || !conflict.Overlaps.EventId.HasValue)
            return conflict;

        var firstEventId = Math.Min(conflict.Entry.EventId.Value, conflict.Overlaps.EventId.Value);
        var secondEventId = Math.Max(conflict.Entry.EventId.Value, conflict.Overlaps.EventId.Value);
        if (!overridesByPair.TryGetValue((firstEventId, secondEventId), out var overrideEntity))
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

    private static (int FirstEventId, int SecondEventId) NormalizePair(int firstEventId, int secondEventId) =>
        (Math.Min(firstEventId, secondEventId), Math.Max(firstEventId, secondEventId));

    private static bool IsPair(CalendarConflict conflict, int firstEventId, int secondEventId) =>
        conflict.Entry.EventId.HasValue
        && conflict.Overlaps.EventId.HasValue
        && Math.Min(conflict.Entry.EventId.Value, conflict.Overlaps.EventId.Value) == firstEventId
        && Math.Max(conflict.Entry.EventId.Value, conflict.Overlaps.EventId.Value) == secondEventId;
}
