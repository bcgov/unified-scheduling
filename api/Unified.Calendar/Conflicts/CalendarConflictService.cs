using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;
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
        var query = new CalendarConflictQuery(
            candidates.Min(candidate => candidate.Start),
            candidates.Max(candidate => candidate.End),
            candidates.Select(candidate => candidate.ResourceId).Distinct().ToList(),
            IncludeDraftParticipants: false
        );
        var existing = (await LoadParticipantsAsync(query, cancellationToken)).Where(participant =>
            !candidateIdentities.Contains(CalendarConflictParticipantIdentity.Create(participant))
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
        Guid createdById,
        CancellationToken cancellationToken = default
    )
    {
        var key = NormalizeAcknowledgement(acknowledgement).Key;
        var firstParticipant = await LoadParticipantAsync(key.FirstEvent, key.ResourceId, cancellationToken);
        var secondParticipant = await LoadParticipantAsync(key.SecondEvent, key.ResourceId, cancellationToken);
        if (firstParticipant is null || secondParticipant is null)
            throw new KeyNotFoundException(
                "Both calendar conflict participants must exist before a conflict can be overridden."
            );

        var rangeStart =
            firstParticipant.Start < secondParticipant.Start ? firstParticipant.Start : secondParticipant.Start;
        var rangeEnd = firstParticipant.End > secondParticipant.End ? firstParticipant.End : secondParticipant.End;
        var participants = await LoadParticipantsAsync(
            new CalendarConflictQuery(rangeStart, rangeEnd, [key.ResourceId]),
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
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var normalizedNote = NormalizeNote(note);
        var now = _timeProvider.GetUtcNow();
        var overrideEntity = await db.CalendarConflictOverrides.SingleOrDefaultAsync(
            candidate =>
                candidate.FirstSourceModule == key.FirstEvent.SourceModule
                && candidate.FirstEventId == key.FirstEvent.EventId
                && candidate.SecondSourceModule == key.SecondEvent.SourceModule
                && candidate.SecondEventId == key.SecondEvent.EventId
                && candidate.ResourceId == key.ResourceId,
            cancellationToken
        );
        if (overrideEntity is null)
        {
            overrideEntity = new CalendarConflictOverride
            {
                FirstSourceModule = key.FirstEvent.SourceModule,
                FirstEventId = key.FirstEvent.EventId,
                SecondSourceModule = key.SecondEvent.SourceModule,
                SecondEventId = key.SecondEvent.EventId,
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
        IReadOnlyCollection<CalendarConflictEventIdentity> eventIdentities,
        Guid? updatedById = null,
        CancellationToken cancellationToken = default
    )
    {
        if (eventIdentities.Count == 0)
            return;

        var identities = eventIdentities
            .Select(identity => CalendarConflictEventIdentity.Create(identity.SourceModule, identity.EventId))
            .ToHashSet();
        var sourceModules = identities.Select(identity => identity.SourceModule).ToList();
        var eventIds = identities.Select(identity => identity.EventId).ToList();
        var possibleOverrides = await db
            .CalendarConflictOverrides.Where(overrideEntity =>
                overrideEntity.InvalidatedOn == null
                && (
                    (
                        sourceModules.Contains(overrideEntity.FirstSourceModule)
                        && eventIds.Contains(overrideEntity.FirstEventId)
                    )
                    || (
                        sourceModules.Contains(overrideEntity.SecondSourceModule)
                        && eventIds.Contains(overrideEntity.SecondEventId)
                    )
                )
            )
            .ToListAsync(cancellationToken);
        var overrides = possibleOverrides
            .Where(overrideEntity =>
                identities.Contains(CreateFirstIdentity(overrideEntity))
                || identities.Contains(CreateSecondIdentity(overrideEntity))
            )
            .ToList();
        if (overrides.Count == 0)
            return;

        var now = _timeProvider.GetUtcNow();
        foreach (var overrideEntity in overrides)
        {
            if (!await IsConflictActiveAsync(overrideEntity, cancellationToken))
                Invalidate(overrideEntity, now, updatedById);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsConflictActiveAsync(
        CalendarConflictOverride overrideEntity,
        CancellationToken cancellationToken
    )
    {
        var key = CalendarConflictKey.Create(
            CreateFirstIdentity(overrideEntity),
            CreateSecondIdentity(overrideEntity),
            overrideEntity.ResourceId
        );
        var firstParticipant = await LoadParticipantAsync(key.FirstEvent, key.ResourceId, cancellationToken);
        var secondParticipant = await LoadParticipantAsync(key.SecondEvent, key.ResourceId, cancellationToken);
        if (firstParticipant is null || secondParticipant is null)
            return false;

        var rangeStart =
            firstParticipant.Start < secondParticipant.Start ? firstParticipant.Start : secondParticipant.Start;
        var rangeEnd = firstParticipant.End > secondParticipant.End ? firstParticipant.End : secondParticipant.End;
        var participants = await LoadParticipantsAsync(
            new CalendarConflictQuery(rangeStart, rangeEnd, [key.ResourceId]),
            cancellationToken
        );
        return CalendarConflictDetector.Detect(participants).Select(CalendarConflictKey.Create).Contains(key);
    }

    private static void Invalidate(CalendarConflictOverride overrideEntity, DateTimeOffset now, Guid? updatedById)
    {
        overrideEntity.InvalidatedOn = now;
        overrideEntity.UpdatedOn = now;
        overrideEntity.UpdatedById = updatedById;
    }

    private async Task<CalendarConflictParticipant?> LoadParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken
    )
    {
        CalendarConflictParticipant? participant = null;
        foreach (var provider in _participantProviders)
        {
            var candidate = await provider.GetParticipantAsync(identity, resourceId, cancellationToken);
            if (candidate is null)
                continue;
            if (candidate.Identity != identity || candidate.ResourceId != resourceId)
                throw new InvalidOperationException(
                    "A calendar conflict provider returned a participant for the wrong identity or resource."
                );
            if (participant is not null)
                throw new InvalidOperationException(
                    "Multiple calendar conflict providers returned the same participant."
                );

            participant = candidate;
        }

        return participant;
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
        var firstEvent = CalendarConflictEventIdentity.Create(
            acknowledgement.FirstSourceModule,
            acknowledgement.FirstEventId
        );
        var secondEvent = CalendarConflictEventIdentity.Create(
            acknowledgement.SecondSourceModule,
            acknowledgement.SecondEventId
        );
        if (firstEvent == secondEvent)
            throw new InvalidOperationException("A conflict acknowledgement requires two different calendar events.");
        if (acknowledgement.ResourceId == Guid.Empty)
            throw new InvalidOperationException("A conflict acknowledgement requires a resource.");

        var normalized = acknowledgement with { Note = NormalizeNote(acknowledgement.Note) };
        return (CalendarConflictKey.Create(firstEvent, secondEvent, normalized.ResourceId), normalized);
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

        var eventIdentities = conflicts
            .SelectMany(conflict => new[] { conflict.Entry.Identity, conflict.Overlaps.Identity })
            .Distinct()
            .ToList();
        var sourceModules = eventIdentities.Select(identity => identity.SourceModule).ToList();
        var eventIds = eventIdentities.Select(identity => identity.EventId).ToList();
        var overrides = await db
            .CalendarConflictOverrides.AsNoTracking()
            .Where(overrideEntity =>
                overrideEntity.InvalidatedOn == null
                && sourceModules.Contains(overrideEntity.FirstSourceModule)
                && eventIds.Contains(overrideEntity.FirstEventId)
                && sourceModules.Contains(overrideEntity.SecondSourceModule)
                && eventIds.Contains(overrideEntity.SecondEventId)
            )
            .ToListAsync(cancellationToken);
        var overridesByConflict = overrides.ToDictionary(overrideEntity =>
            CalendarConflictKey.Create(
                CreateFirstIdentity(overrideEntity),
                CreateSecondIdentity(overrideEntity),
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

    private static CalendarConflictEventIdentity CreateFirstIdentity(CalendarConflictOverride overrideEntity) =>
        CalendarConflictEventIdentity.Create(overrideEntity.FirstSourceModule, overrideEntity.FirstEventId);

    private static CalendarConflictEventIdentity CreateSecondIdentity(CalendarConflictOverride overrideEntity) =>
        CalendarConflictEventIdentity.Create(overrideEntity.SecondSourceModule, overrideEntity.SecondEventId);

    private readonly record struct CalendarConflictParticipantIdentity(
        CalendarConflictEventIdentity EventIdentity,
        Guid ResourceId
    )
    {
        public static CalendarConflictParticipantIdentity Create(CalendarConflictParticipant participant) =>
            new(participant.Identity, participant.ResourceId);
    }
}
