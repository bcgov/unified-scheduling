using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Interceptors;

public sealed class SchedulingConcurrencyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (!result.HasResult && eventData.Context is UnifiedDbContext db)
            PropagateAsync(db, asyncSave: false, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (!result.HasResult && eventData.Context is UnifiedDbContext db)
            await PropagateAsync(db, asyncSave: true, cancellationToken);
        return result;
    }

    private static async ValueTask PropagateAsync(
        UnifiedDbContext db,
        bool asyncSave,
        CancellationToken cancellationToken
    )
    {
        var changes = db.ChangeTracker.Entries().Where(HasChanges).ToArray();
        HashSet<int> shiftIds = [],
            assignmentIds = [],
            shiftSeriesIds = [],
            assignmentSeriesIds = [];
        HashSet<int> entryLinkIds = [],
            seriesLinkIds = [];
        HashSet<int> shiftEventIds = [],
            assignmentEventIds = [];
        HashSet<int> shiftEventSeriesIds = [],
            assignmentEventSeriesIds = [];
        HashSet<int> deletedShiftSeriesIds = [],
            deletedAssignmentSeriesIds = [];

        // Snapshot both FK values: reassignment must invalidate the old and new owners.
        foreach (var entry in changes)
        {
            switch (entry.Entity)
            {
                case ShiftAssignmentEntry:
                    AddIds(entry, nameof(ShiftAssignmentEntry.Id), entryLinkIds);
                    AddIds(entry, nameof(ShiftAssignmentEntry.ShiftEntryId), shiftIds);
                    AddIds(entry, nameof(ShiftAssignmentEntry.AssignmentEntryId), assignmentIds);
                    break;
                case ShiftAssignmentEntryUser:
                    AddIds(entry, nameof(ShiftAssignmentEntryUser.ShiftAssignmentEntryId), entryLinkIds);
                    break;
                case ShiftAssignmentSeriesLink:
                    AddIds(entry, nameof(ShiftAssignmentSeriesLink.Id), seriesLinkIds);
                    AddIds(entry, nameof(ShiftAssignmentSeriesLink.ShiftSeriesId), shiftSeriesIds);
                    AddIds(entry, nameof(ShiftAssignmentSeriesLink.AssignmentSeriesId), assignmentSeriesIds);
                    break;
                case ShiftAssignmentSeriesLinkUser:
                    AddIds(entry, nameof(ShiftAssignmentSeriesLinkUser.ShiftAssignmentSeriesLinkId), seriesLinkIds);
                    break;
                case ShiftEntryUser:
                    AddIds(entry, nameof(ShiftEntryUser.ShiftEntryId), shiftIds);
                    break;
                case ShiftSeriesUser:
                    AddIds(entry, nameof(ShiftSeriesUser.ShiftSeriesId), shiftSeriesIds);
                    break;
                case ShiftEntry:
                    AddIds(entry, nameof(ShiftEntry.Id), shiftIds);
                    AddIds(entry, nameof(ShiftEntry.ShiftSeriesId), shiftSeriesIds);
                    break;
                case AssignmentEntry:
                    AddIds(entry, nameof(AssignmentEntry.Id), assignmentIds);
                    AddIds(entry, nameof(AssignmentEntry.AssignmentSeriesId), assignmentSeriesIds);
                    break;
                case ShiftSeries:
                    if (entry.State == EntityState.Deleted)
                        AddIds(entry, nameof(ShiftSeries.Id), deletedShiftSeriesIds);
                    break;
                case AssignmentSeries:
                    if (entry.State == EntityState.Deleted)
                        AddIds(entry, nameof(AssignmentSeries.Id), deletedAssignmentSeriesIds);
                    break;
                case Event when entry.State != EntityState.Added:
                    AddEventIds(entry, shiftEventIds, assignmentEventIds);
                    break;
                case EventSeries when entry.State != EntityState.Added:
                    // EventSeries has no SourceModule; Scheduling owns these two event type codes.
                    AddEventIds(entry, shiftEventSeriesIds, assignmentEventSeriesIds);
                    break;
            }
        }

        if (entryLinkIds.Count > 0)
        {
            await LoadAsync(
                db.ShiftAssignmentEntries.Where(link => entryLinkIds.Contains(link.Id)),
                asyncSave,
                cancellationToken
            );
            foreach (var entry in db.ChangeTracker.Entries<ShiftAssignmentEntry>())
            {
                if (!entryLinkIds.Contains(entry.Entity.Id))
                    continue;
                AddIds(entry, nameof(ShiftAssignmentEntry.ShiftEntryId), shiftIds);
                AddIds(entry, nameof(ShiftAssignmentEntry.AssignmentEntryId), assignmentIds);
            }
        }
        if (seriesLinkIds.Count > 0)
        {
            await LoadAsync(
                db.ShiftAssignmentSeriesLinks.Where(link => seriesLinkIds.Contains(link.Id)),
                asyncSave,
                cancellationToken
            );
            foreach (var entry in db.ChangeTracker.Entries<ShiftAssignmentSeriesLink>())
            {
                if (!seriesLinkIds.Contains(entry.Entity.Id))
                    continue;
                AddIds(entry, nameof(ShiftAssignmentSeriesLink.ShiftSeriesId), shiftSeriesIds);
                AddIds(entry, nameof(ShiftAssignmentSeriesLink.AssignmentSeriesId), assignmentSeriesIds);
            }
        }

        // Loading surviving children makes database-side SET NULL on series deletion visible
        // to EF, so their versions advance too. Link deletions are explicit (Restrict FKs).
        if (shiftIds.Count > 0 || shiftEventIds.Count > 0 || deletedShiftSeriesIds.Count > 0)
        {
            await LoadAsync(
                db.ShiftEntries.Where(root =>
                    shiftIds.Contains(root.Id)
                    || shiftEventIds.Contains(root.EventId)
                    || (root.ShiftSeriesId.HasValue && deletedShiftSeriesIds.Contains(root.ShiftSeriesId.Value))
                ),
                asyncSave,
                cancellationToken
            );
            foreach (var entry in db.ChangeTracker.Entries<ShiftEntry>().ToArray())
            {
                if (
                    !shiftIds.Contains(entry.Entity.Id)
                    && !shiftEventIds.Contains(entry.Entity.EventId)
                    && !ReferencesDeletedSeries(entry, nameof(ShiftEntry.ShiftSeriesId), deletedShiftSeriesIds)
                )
                    continue;
                AddIds(entry, nameof(ShiftEntry.ShiftSeriesId), shiftSeriesIds);
                db.TouchForConcurrency(entry.Entity);
            }
        }
        if (assignmentIds.Count > 0 || assignmentEventIds.Count > 0 || deletedAssignmentSeriesIds.Count > 0)
        {
            await LoadAsync(
                db.AssignmentEntries.Where(root =>
                    assignmentIds.Contains(root.Id)
                    || assignmentEventIds.Contains(root.EventId)
                    || (
                        root.AssignmentSeriesId.HasValue
                        && deletedAssignmentSeriesIds.Contains(root.AssignmentSeriesId.Value)
                    )
                ),
                asyncSave,
                cancellationToken
            );
            foreach (var entry in db.ChangeTracker.Entries<AssignmentEntry>().ToArray())
            {
                if (
                    !assignmentIds.Contains(entry.Entity.Id)
                    && !assignmentEventIds.Contains(entry.Entity.EventId)
                    && !ReferencesDeletedSeries(
                        entry,
                        nameof(AssignmentEntry.AssignmentSeriesId),
                        deletedAssignmentSeriesIds
                    )
                )
                    continue;
                AddIds(entry, nameof(AssignmentEntry.AssignmentSeriesId), assignmentSeriesIds);
                db.TouchForConcurrency(entry.Entity);
            }
        }
        if (shiftSeriesIds.Count > 0 || shiftEventSeriesIds.Count > 0)
        {
            var roots = await LoadAsync(
                db.ShiftSeries.Where(root =>
                    shiftSeriesIds.Contains(root.Id) || shiftEventSeriesIds.Contains(root.EventSeriesId)
                ),
                asyncSave,
                cancellationToken
            );
            foreach (var root in roots)
                db.TouchForConcurrency(root);
        }
        if (assignmentSeriesIds.Count > 0 || assignmentEventSeriesIds.Count > 0)
        {
            var roots = await LoadAsync(
                db.AssignmentSeries.Where(root =>
                    assignmentSeriesIds.Contains(root.Id) || assignmentEventSeriesIds.Contains(root.EventSeriesId)
                ),
                asyncSave,
                cancellationToken
            );
            foreach (var root in roots)
                db.TouchForConcurrency(root);
        }
    }

    private static bool HasChanges(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Deleted
        || (
            entry.State == EntityState.Modified
            && entry.Properties.Any(property =>
                property.IsModified
                && property.Metadata.Name
                    is not (
                        nameof(BaseEntity.UpdatedOn)
                        or nameof(BaseEntity.UpdatedById)
                        or nameof(BaseEntity.CreatedOn)
                        or nameof(BaseEntity.CreatedById)
                        or nameof(BaseEntity.ConcurrencyToken)
                    )
                && !Equals(property.OriginalValue, property.CurrentValue)
            )
        );

    private static void AddIds(EntityEntry entry, string propertyName, HashSet<int> ids)
    {
        var property = entry.Property(propertyName);
        if (entry.State != EntityState.Added && property.OriginalValue is int original && original > 0)
            ids.Add(original);
        if (entry.State != EntityState.Deleted && property.CurrentValue is int current && current > 0)
            ids.Add(current);
    }

    private static void AddEventIds(EntityEntry entry, HashSet<int> shiftIds, HashSet<int> assignmentIds)
    {
        var type = entry.Property(nameof(Event.EventTypeCode));
        var source = entry.Entity is Event ? entry.Property(nameof(Event.SourceModule)) : null;
        if (source is null || Equals(source.OriginalValue, SchedulingConstants.SourceModule))
            AddOwnedType(type.OriginalValue);
        if (source is null || Equals(source.CurrentValue, SchedulingConstants.SourceModule))
            AddOwnedType(type.CurrentValue);

        void AddOwnedType(object? code)
        {
            if (Equals(code, SchedulingConstants.ShiftEventTypeCode))
                AddIds(entry, nameof(Event.Id), shiftIds);
            if (Equals(code, SchedulingConstants.AssignmentEventTypeCode))
                AddIds(entry, nameof(Event.Id), assignmentIds);
        }
    }

    private static bool ReferencesDeletedSeries(EntityEntry entry, string propertyName, HashSet<int> ids) =>
        entry.Property(propertyName).OriginalValue is int id && ids.Contains(id);

    private static ValueTask<List<T>> LoadAsync<T>(
        IQueryable<T> query,
        bool asyncSave,
        CancellationToken cancellationToken
    ) =>
        // The synchronous path uses only completed ValueTasks and synchronous database I/O.
        asyncSave ? new(query.ToListAsync(cancellationToken)) : new(query.ToList());
}
