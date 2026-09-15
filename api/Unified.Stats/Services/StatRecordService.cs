using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Infrastructure.ErrorHandling;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatRecordService(UnifiedDbContext db, ILogger<StatRecordService> logger) : IStatRecordService
{
    public async Task<IReadOnlyCollection<StatRecordResponse>> GetAllAsync(
        StatRecordQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug(
            "Retrieving stat records for location {LocationId}, metric {SubCategoryMetricId}, status {Status}, user {UserId}",
            queryParams?.LocationId,
            queryParams?.SubCategoryMetricId,
            queryParams?.Status,
            queryParams?.UserId
        );

        var query = db.StatRecords.AsNoTracking();

        if (queryParams?.LocationId is int locationId)
            query = query.Where(r => r.LocationId == locationId);

        if (queryParams?.GroupId is int groupId)
            query = query.Where(r => r.SubCategoryMetric!.SubCategory!.Category!.GroupId == groupId);

        if (queryParams?.SubCategoryMetricId is int subCategoryMetricId)
            query = query.Where(r => r.SubCategoryMetricId == subCategoryMetricId);

        if (queryParams?.PeriodType is { Length: > 0 } periodType)
            query = query.Where(r => r.PeriodType == periodType);

        if (queryParams?.FromDate is DateOnly fromDate)
            query = query.Where(r => r.DateFrom >= fromDate);

        if (queryParams?.ToDate is DateOnly toDate)
            query = query.Where(r => r.DateTo <= toDate);

        if (queryParams?.Status is { Length: > 0 } status)
            query = query.Where(r => r.Status == status);

        if (queryParams?.UserId is Guid userId)
            query = query.Where(r => r.UserId == userId);

        return await query
            .OrderByDescending(r => r.DateFrom)
            .ProjectToType<StatRecordResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatRecordResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat record {StatRecordId}", id);

        return await db
            .StatRecords.AsNoTracking()
            .Where(r => r.Id == id)
            .ProjectToType<StatRecordResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StatRecordResponse> CreateAsync(
        StatRecordRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Creating stat record for user {UserId}, location {LocationId}, metric {SubCategoryMetricId}, caller {CallerUserId}",
            request.UserId,
            request.LocationId,
            request.SubCategoryMetricId,
            callerUserId
        );

        EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        var entity = MapToEntity(request);
        db.StatRecords.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created stat record {StatRecordId}", entity.Id);

        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(
        IReadOnlyList<StatRecordRequest> requests,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Creating {RecordCount} stat records for caller {CallerUserId}",
            requests.Count,
            callerUserId
        );

        foreach (var request in requests)
            EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        var entities = requests.Select(MapToEntity).ToList();
        db.StatRecords.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created {RecordCount} stat records", entities.Count);

        return entities.Adapt<List<StatRecordResponse>>();
    }

    public async Task<StatRecordResponse?> UpdateAsync(
        int id,
        StatRecordRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Updating stat record {StatRecordId} request user {RequestUserId} caller {CallerUserId}",
            id,
            request.UserId,
            callerUserId
        );

        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogDebug("Stat record {StatRecordId} was not found for update", id);
            return null;
        }

        // Check caller is allowed to modify the existing record's owner, then the new owner.
        EnsureAuthorizedToSubmitFor(entity.UserId, callerUserId, callerCanEnterForOthers);
        EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        entity.DateFrom = request.DateFrom;
        entity.DateTo = request.DateTo;
        entity.PeriodType = request.PeriodType.Trim();
        entity.UserId = request.UserId;
        entity.LocationId = request.LocationId;
        entity.SubCategoryMetricId = request.SubCategoryMetricId;
        entity.Value = request.Value;
        entity.Comment = request.Comment?.Trim();
        entity.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated stat record {StatRecordId}", id);

        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<bool> DeleteAsync(
        int id,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Deleting stat record {StatRecordId} caller {CallerUserId}", id, callerUserId);

        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogDebug("Stat record {StatRecordId} was not found for delete", id);
            return false;
        }

        EnsureAuthorizedToSubmitFor(entity.UserId, callerUserId, callerCanEnterForOthers);

        db.StatRecords.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted stat record {StatRecordId}", id);

        return true;
    }

    public async Task<IReadOnlyCollection<StatRecordResponse>> SaveDayAsync(
        SaveDayRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        bool canOverrideSignedOff = false,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Saving {RecordCount} stat records for user {UserId}, location {LocationId}, date {Date}, caller {CallerUserId}",
            request.Records.Count,
            request.UserId,
            request.LocationId,
            request.Date,
            callerUserId
        );

        if (request.Records.Count == 0)
            throw new ValidationException("At least one record is required.");

        // Verify whether this group is location-level from the database, not the request.
        var isLocationLevel = await db.StatGroups.AnyAsync(
            g => g.Id == request.GroupId && g.IsLocationLevel,
            cancellationToken
        );

        // Location-level entries are per-location, not per-employee — skip user auth.
        if (!isLocationLevel)
            EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        // Verify all submitted metrics belong to the requested group to prevent
        // location-level metrics from being entered via employee forms and vice versa.
        var requestedMetricIds = request.Records.Select(r => r.SubCategoryMetricId).Distinct().ToList();
        var metricUnits = await db
            .SubCategoryMetrics.Where(scm =>
                requestedMetricIds.Contains(scm.Id)
                && !scm.IsArchived
                && scm.SubCategory!.Category!.GroupId == request.GroupId
            )
            .Select(scm => new { scm.Id, scm.Metric!.UnitOfMeasure })
            .ToListAsync(cancellationToken);
        if (metricUnits.Count != requestedMetricIds.Count)
            throw new ValidationException("One or more metrics do not belong to the requested group.");

        var unitByMetricId = metricUnits.ToDictionary(m => m.Id, m => m.UnitOfMeasure);
        ValidateRecordValues(request.Records, unitByMetricId);

        // Load existing records for this user/location/date scoped to the same group so we can
        // diff within one transaction without touching records that belong to other group forms.
        var existingQuery = db.StatRecords.Where(r =>
            r.LocationId == request.LocationId
            && r.DateFrom == request.Date
            && r.DateTo == request.Date
            && r.SubCategoryMetric!.SubCategory!.Category!.GroupId == request.GroupId
        );

        // Location-level records have no UserId; employee forms filter by user.
        existingQuery = isLocationLevel
            ? existingQuery.Where(r => r.UserId == null)
            : existingQuery.Where(r => r.UserId == request.UserId);

        var existingRecords = await existingQuery.ToListAsync(cancellationToken);

        var incomingIds = request.Records.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToHashSet();

        // Delete records no longer present in the incoming set; signed-off records are immutable
        // unless the caller has the override permission.
        var toDelete = existingRecords
            .Where(r => !incomingIds.Contains(r.Id) && (canOverrideSignedOff || r.Status != StatRecordStatus.SignedOff))
            .ToList();
        db.StatRecords.RemoveRange(toDelete);
        logger.LogDebug("Deleting {RecordCount} stale stat records while saving day", toDelete.Count);

        var results = new List<StatRecord>();

        foreach (var item in request.Records)
        {
            if (item.Id.HasValue)
            {
                // Update existing record
                var entity = existingRecords.FirstOrDefault(r => r.Id == item.Id.Value);
                if (entity is null || (!canOverrideSignedOff && entity.Status == StatRecordStatus.SignedOff))
                {
                    logger.LogDebug("Skipping stale stat record id {StatRecordId} while saving day", item.Id.Value);
                    continue;
                }

                entity.SubCategoryMetricId = item.SubCategoryMetricId;
                entity.Value = item.Value ?? 0;
                entity.Comment = item.Comment?.Trim();
                entity.Status = request.Status;
                entity.PerformedAtLocationId = item.PerformedAtLocationId;
                results.Add(entity);
            }
            else
            {
                // Create new record — location-level entries have no UserId
                var entity = new StatRecord
                {
                    DateFrom = request.Date,
                    DateTo = request.Date,
                    PeriodType = "Daily",
                    UserId = isLocationLevel ? null : request.UserId,
                    LocationId = request.LocationId,
                    PerformedAtLocationId = item.PerformedAtLocationId,
                    SubCategoryMetricId = item.SubCategoryMetricId,
                    Value = item.Value ?? 0,
                    Comment = item.Comment?.Trim(),
                    Status = request.Status,
                };
                db.StatRecords.Add(entity);
                results.Add(entity);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Saved {RecordCount} stat records for user {UserId}, location {LocationId}, date {Date}",
            results.Count,
            request.UserId,
            request.LocationId,
            request.Date
        );

        return results.Adapt<List<StatRecordResponse>>();
    }

    public async Task<bool> IsLocationLevelGroupAsync(int groupId, CancellationToken cancellationToken = default) =>
        await db.StatGroups.AnyAsync(g => g.Id == groupId && g.IsLocationLevel, cancellationToken);

    private static void EnsureAuthorizedToSubmitFor(
        Guid? requestedUserId,
        Guid callerUserId,
        bool callerCanEnterForOthers
    )
    {
        if (!callerCanEnterForOthers && requestedUserId != callerUserId)
            throw new ForbiddenException();
    }

    private static void ValidateRecordValues(
        IReadOnlyList<SaveDayRecordItem> records,
        Dictionary<int, string> unitByMetricId
    )
    {
        foreach (var record in records)
        {
            if (record.Value is not { } value || value == 0)
                continue;

            if (value < 0)
                throw new ValidationException($"Value must not be negative (metric {record.SubCategoryMetricId}).");

            if (!unitByMetricId.TryGetValue(record.SubCategoryMetricId, out var unit))
                continue;

            switch (unit)
            {
                case "hours" when value % 0.25m != 0:
                    throw new ValidationException(
                        $"Hours must be in 0.25 increments (metric {record.SubCategoryMetricId})."
                    );
                case "count" or "count (received/concluded)" when value != Math.Truncate(value):
                    throw new ValidationException(
                        $"Count values must be whole numbers (metric {record.SubCategoryMetricId})."
                    );
                case "$" when value * 100 != Math.Truncate(value * 100):
                    throw new ValidationException(
                        $"Dollar values must be in 0.01 increments (metric {record.SubCategoryMetricId})."
                    );
            }
        }
    }

    private static StatRecord MapToEntity(StatRecordRequest request) =>
        new()
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            PeriodType = request.PeriodType.Trim(),
            UserId = request.UserId,
            LocationId = request.LocationId,
            SubCategoryMetricId = request.SubCategoryMetricId,
            Value = request.Value,
            Comment = request.Comment?.Trim(),
            Status = request.Status,
        };
}
