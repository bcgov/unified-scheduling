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
    ) => await ValidateAndApplyConflictAcknowledgementsAsync(candidates, null, null, cancellationToken);

    public async Task ValidateAndApplyConflictAcknowledgementsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements,
        Guid? actorId,
        CancellationToken cancellationToken = default
    )
    {
        var acknowledgementsByKey = NormalizeAcknowledgements(acknowledgements);
        if (acknowledgementsByKey.Count > 0 && !actorId.HasValue)
            throw new InvalidOperationException("An authenticated actor is required to override calendar conflicts.");

        if (candidates.Count == 0)
            return;

        var candidateIdentities = candidates.Select(CalendarConflictParticipantIdentity.Create).ToHashSet();
        var candidateEventIds = candidates.Select(candidate => candidate.EventId).ToHashSet();
        var query = new CalendarConflictQuery(
            candidates.Min(candidate => candidate.Start),
            candidates.Max(candidate => candidate.End),
            candidates.Select(candidate => candidate.ResourceId).Distinct().ToList(),
            IncludeDraftParticipants: false
        );
        var existing = (await LoadParticipantsAsync(query, cancellationToken)).Where(participant =>
            !candidateEventIds.Contains(participant.EventId)
        );
        var allParticipants = existing
            .Concat(candidates)
            .GroupBy(CalendarConflictParticipantIdentity.Create)
            .Select(group => group.Last())
            .ToList();
        var conflicts = CalendarConflictDetector
            .Detect(allParticipants)
            .Where(conflict =>
                candidateIdentities.Contains(CalendarConflictParticipantIdentity.Create(conflict.Entry))
                || candidateIdentities.Contains(CalendarConflictParticipantIdentity.Create(conflict.Overlaps))
            )
            .ToList();

        var conflictsWithOverrides = await ApplyOverridesAsync(conflicts, cancellationToken);
        var currentConflictsByKey = conflictsWithOverrides.ToDictionary(CalendarConflictKey.Create);
        var unresolved = conflictsWithOverrides
            .Where(conflict =>
                !conflict.IsOverridden && !acknowledgementsByKey.ContainsKey(CalendarConflictKey.Create(conflict))
            )
            .ToList();
        if (unresolved.Count > 0)
            throw new CalendarConflictException(unresolved);

        var acknowledgementsToPersist = acknowledgementsByKey
            .Where(item => currentConflictsByKey.TryGetValue(item.Key, out var conflict) && !conflict.IsOverridden)
            .ToList();
        if (acknowledgementsToPersist.Count == 0)
            return;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Conflict acknowledgements must be persisted inside the mutation transaction."
            );

        foreach (var (key, acknowledgement) in acknowledgementsToPersist)
            await UpsertOverrideAsync(key, acknowledgement.Note, actorId!.Value, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateOverrideAsync(
        CalendarConflictAcknowledgement acknowledgement,
        Guid? createdById,
        CancellationToken cancellationToken = default
    )
    {
        var key = NormalizeAcknowledgement(acknowledgement).Key;
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

        await UpsertOverrideAsync(key, acknowledgement.Note, createdById, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CalendarConflictOverride> UpsertOverrideAsync(
        CalendarConflictKey key,
        string note,
        Guid? actorId,
        CancellationToken cancellationToken
    )
    {
        var normalizedNote = NormalizeNote(note);
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
                Note = normalizedNote,
                CreatedById = actorId,
                CreatedOn = now,
            };
            db.CalendarConflictOverrides.Add(overrideEntity);
        }
        else
        {
            overrideEntity.Note = normalizedNote;
            overrideEntity.InvalidatedOn = null;
            overrideEntity.UpdatedById = actorId;
            overrideEntity.UpdatedOn = now;
        }

        return overrideEntity;
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
                overrideEntity.InvalidatedOn == null
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
        var participants = new List<CalendarConflictParticipant>();
        foreach (var provider in _participantProviders)
            participants.AddRange(await provider.GetParticipantsAsync(query, cancellationToken));

        return participants;
    }

    private static IReadOnlyDictionary<CalendarConflictKey, CalendarConflictAcknowledgement> NormalizeAcknowledgements(
        IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements
    )
    {
        var normalized = new Dictionary<CalendarConflictKey, CalendarConflictAcknowledgement>();
        foreach (var acknowledgement in acknowledgements ?? [])
        {
            var item = NormalizeAcknowledgement(acknowledgement);
            if (normalized.TryGetValue(item.Key, out var existing) && existing.Note != item.Acknowledgement.Note)
                throw new InvalidOperationException("Duplicate conflict acknowledgements must use the same note.");

            normalized[item.Key] = item.Acknowledgement;
        }

        return normalized;
    }

    private static (CalendarConflictKey Key, CalendarConflictAcknowledgement Acknowledgement) NormalizeAcknowledgement(
        CalendarConflictAcknowledgement acknowledgement
    )
    {
        if (acknowledgement.FirstEventId <= 0 || acknowledgement.SecondEventId <= 0)
            throw new InvalidOperationException("A conflict acknowledgement requires two valid calendar events.");
        if (acknowledgement.FirstEventId == acknowledgement.SecondEventId)
            throw new InvalidOperationException("A conflict acknowledgement requires two different calendar events.");
        if (acknowledgement.ResourceId == Guid.Empty)
            throw new InvalidOperationException("A conflict acknowledgement requires a resource.");

        var normalized = acknowledgement with { Note = NormalizeNote(acknowledgement.Note) };
        return (
            CalendarConflictKey.Create(normalized.FirstEventId, normalized.SecondEventId, normalized.ResourceId),
            normalized
        );
    }

    private static string NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A conflict override note is required.");

        var normalized = note.Trim();
        if (normalized.Length > 2000)
            throw new InvalidOperationException("A conflict override note cannot exceed 2000 characters.");

        return normalized;
    }

    private async Task<IReadOnlyCollection<CalendarConflict>> ApplyOverridesAsync(
        IReadOnlyCollection<CalendarConflict> conflicts,
        CancellationToken cancellationToken
    )
    {
        if (conflicts.Count == 0)
            return conflicts;

        var eventIds = conflicts
            .SelectMany(conflict => new[] { conflict.Entry.EventId, conflict.Overlaps.EventId })
            .Distinct()
            .ToList();
        var overrides = await db
            .CalendarConflictOverrides.AsNoTracking()
            .Where(overrideEntity =>
                overrideEntity.InvalidatedOn == null
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
        var key = CalendarConflictKey.Create(conflict);
        if (!overridesByConflict.TryGetValue(key, out var overrideEntity))
            return conflict;

        return conflict with
        {
            IsOverridden = true,
            OverrideNote = overrideEntity.Note,
            CreatedById = overrideEntity.CreatedById,
            CreatedOn = overrideEntity.CreatedOn,
            UpdatedById = overrideEntity.UpdatedById,
            UpdatedOn = overrideEntity.UpdatedOn,
        };
    }

    private readonly record struct CalendarConflictParticipantIdentity(int EventId, Guid ResourceId)
    {
        public static CalendarConflictParticipantIdentity Create(CalendarConflictParticipant participant) =>
            new(participant.EventId, participant.ResourceId);
    }
}
