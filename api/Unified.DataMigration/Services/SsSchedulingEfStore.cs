using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services;

/// <summary>PerRow Scheduling target access: the original EF Core queries and saves, unchanged.</summary>
internal sealed class SsSchedulingEfStore(UnifiedDbContext db) : ISsSchedulingStore
{
    public Task<List<string>> RetainedMemberIdsAsync(CancellationToken ct) =>
        db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.EntityType == "SchedulingMember")
            .Select(x => x.LegacyId)
            .ToListAsync(ct);

    public Task<bool> ClassificationExistsAsync(int subcategory, int category, CancellationToken ct) =>
        db.SubCategories.AnyAsync(
            x => x.Id == subcategory && x.CategoryId == category && x.Category != null && x.Category.GroupId != 3,
            ct
        );

    public Task<List<CourtRoom>> RoomsAsync(string code, int? location, CancellationToken ct) =>
        db.CourtRooms.Where(x => x.Room == code && x.LocationId == location).ToListAsync(ct);

    public async Task AddRoomAsync(CourtRoom room, CancellationToken ct)
    {
        db.CourtRooms.Add(room);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<DataMigrationRecord>> RecordsAsync(
        string type,
        IReadOnlyCollection<string> legacyIds,
        CancellationToken ct
    ) =>
        db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == type && legacyIds.Contains(x.LegacyId))
            .ToListAsync(ct);

    public Task<bool> DefinitionNameTakenAsync(
        int location,
        string normalized,
        int[] exceptIds,
        CancellationToken ct
    ) =>
        db.AssignmentDefinitions.AnyAsync(
            x => x.LocationId == location && x.NormalizedName == normalized && !exceptIds.Contains(x.Id),
            ct
        );

    public async Task AddDefinitionAsync(AssignmentDefinition definition, CancellationToken ct)
    {
        db.AssignmentDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
    }

    public Task<AssignmentDefinition> DefinitionAsync(int id, CancellationToken ct) =>
        db.AssignmentDefinitions.SingleAsync(x => x.Id == id, ct);

    public Task<User> UserAsync(Guid id, CancellationToken ct) => db.Users.SingleAsync(x => x.Id == id, ct);

    public Task<bool> ShiftOnDayExistsAsync(
        int location,
        Guid user,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    ) =>
        db.ShiftEntries.AnyAsync(
            x =>
                x.Event != null
                && x.Event.LocationId == location
                && x.Event.StatusTypeCode != "cancelled"
                && x.Event.StartAtUtc >= from
                && x.Event.StartAtUtc < to
                && x.Users.Any(y => y.UserId == user),
            ct
        );

    public async Task AddShiftAsync(ShiftEntry shift, CancellationToken ct)
    {
        db.ShiftEntries.Add(shift);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<(string LegacyId, string TargetKey)>> ResolvedShiftsAsync(
        string[] legacyIds,
        CancellationToken ct
    ) =>
        (
            await db
                .DataMigrationRecords.AsNoTracking()
                .Where(x => x.Source == "SS" && x.EntityType == "Shift" && legacyIds.Contains(x.LegacyId))
                .OrderBy(x => x.LegacyId)
                .Select(x => new { x.LegacyId, x.TargetKey })
                .ToListAsync(ct)
        )
            .Select(x => (x.LegacyId, x.TargetKey))
            .ToList();

    public async Task AddAssignmentEntryAsync(AssignmentEntry entry, CancellationToken ct)
    {
        db.AssignmentEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<ShiftEntry>> OverlappingShiftsAsync(
        int location,
        Guid user,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    ) =>
        db
            .ShiftEntries.Where(x =>
                x.Event != null
                && x.Event.LocationId == location
                && x.Event.StatusTypeCode != "cancelled"
                && x.Event.StartAtUtc < end
                && x.Event.EndAtUtc > start
                && x.Users.Any(y => y.UserId == user)
            )
            .ToListAsync(ct);

    public Task<bool> FutureAssignmentConflictAsync(
        Guid user,
        int entryId,
        int definitionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    ) =>
        db.ShiftAssignmentEntryUsers.AnyAsync(
            x =>
                x.UserId == user
                && x.ShiftAssignmentEntry!.AssignmentEntryId != entryId
                && x.ShiftAssignmentEntry.AssignmentEntry!.AssignmentDefinitionId == definitionId
                && x.ShiftAssignmentEntry.AssignmentEntry.Event!.StatusTypeCode != "cancelled"
                && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc >= from
                && x.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc < to,
            ct
        );

    public Task<ShiftAssignmentEntry?> ShiftAssignmentAsync(int shiftId, int entryId, CancellationToken ct) =>
        db
            .ShiftAssignmentEntries.Include(x => x.Users)
            .SingleOrDefaultAsync(x => x.ShiftEntryId == shiftId && x.AssignmentEntryId == entryId, ct);

    public void AddShiftAssignment(ShiftAssignmentEntry link) => db.ShiftAssignmentEntries.Add(link);

    public void AddShiftAssignmentUser(ShiftAssignmentEntry link, ShiftAssignmentEntryUser user) =>
        link.Users.Add(user);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<bool> AnyContainingShiftAsync(
        int[] ids,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    ) =>
        db.ShiftEntries.AnyAsync(
            x => ids.Contains(x.Id) && x.Event!.StartAtUtc <= start && x.Event.EndAtUtc >= end,
            ct
        );

    public Task<bool> RecordExistsAsync(string type, string legacyId, CancellationToken ct) =>
        db.DataMigrationRecords.AnyAsync(x => x.Source == "SS" && x.EntityType == type && x.LegacyId == legacyId, ct);

    public Task<DataMigrationRecord?> RecordAsync(string type, string legacyId, CancellationToken ct) =>
        db.DataMigrationRecords.SingleOrDefaultAsync(
            x => x.Source == "SS" && x.EntityType == type && x.LegacyId == legacyId,
            ct
        );

    public Task<DataMigrationOutcome?> PriorOutcomeAsync(
        Guid? runId,
        string type,
        string legacyId,
        CancellationToken ct
    ) =>
        db.DataMigrationOutcomes.SingleOrDefaultAsync(
            x => x.RunId == runId && x.EntityType == type && x.LegacyId == legacyId,
            ct
        );

    public Task<AssignmentEntry> AssignmentEntryWithEventAsync(int id, CancellationToken ct) =>
        db.AssignmentEntries.Include(x => x.Event).SingleAsync(x => x.Id == id, ct);

    public async Task AddSeriesAsync(
        AssignmentSeries series,
        IReadOnlyList<AssignmentEntry> members,
        CancellationToken ct
    )
    {
        db.AssignmentSeries.Add(series);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<DataMigrationRecord>> SchedulingMembersAsync(CancellationToken ct) =>
        db.DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == "SchedulingMember").ToListAsync(ct);

    public Task<List<DataMigrationRecord>> RootRecordsAsync(IReadOnlyCollection<string> types, CancellationToken ct) =>
        db
            .DataMigrationRecords.TagWith("SS migration initial capture root index")
            .AsNoTracking()
            .Where(x => x.Source == "SS" && types.Contains(x.EntityType))
            .ToListAsync(ct);

    public Task<List<DataMigrationRecord>> RecordsByIdAsync(long[] ids, CancellationToken ct) =>
        db
            .DataMigrationRecords.TagWith("SS migration capture selected roots")
            .Where(x => x.Source == "SS" && ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public void Detach() => db.ChangeTracker.Clear();

    public async Task FlushAndDetachAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<bool> HasUnownedDependentsAsync(
        int[] definitions,
        int[] shifts,
        int[] entries,
        int[] series,
        CancellationToken ct
    ) =>
        await db.AssignmentEntries.AnyAsync(
            x =>
                (
                    definitions.Contains(x.AssignmentDefinitionId)
                    || x.AssignmentSeriesId != null && series.Contains(x.AssignmentSeriesId.Value)
                ) && !entries.Contains(x.Id),
            ct
        )
        || await db.AssignmentSeries.AnyAsync(
            x => definitions.Contains(x.AssignmentDefinitionId) && !series.Contains(x.Id),
            ct
        )
        || await db.ShiftAssignmentEntries.AnyAsync(
            x =>
                (shifts.Contains(x.ShiftEntryId) && !entries.Contains(x.AssignmentEntryId))
                || (entries.Contains(x.AssignmentEntryId) && !shifts.Contains(x.ShiftEntryId)),
            ct
        )
        || await db.ShiftAssignmentSeriesLinks.AnyAsync(x => series.Contains(x.AssignmentSeriesId), ct);
}
