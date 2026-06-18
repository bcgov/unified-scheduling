using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Infrastructure.ErrorHandling;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatRecordService(UnifiedDbContext db) : IStatRecordService
{
    public async Task<IReadOnlyCollection<StatRecordResponse>> GetAllAsync(
        StatRecordQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.StatRecords.AsNoTracking();

        if (queryParams?.LocationId is int locationId)
            query = query.Where(r => r.LocationId == locationId);

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
        EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        var entity = MapToEntity(request);
        db.StatRecords.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(
        IReadOnlyList<StatRecordRequest> requests,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var request in requests)
            EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        var entities = requests.Select(MapToEntity).ToList();
        db.StatRecords.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
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
        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return null;

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
        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<bool> DeleteAsync(
        int id,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return false;

        EnsureAuthorizedToSubmitFor(entity.UserId, callerUserId, callerCanEnterForOthers);

        db.StatRecords.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<StatRecordResponse>> SaveDayAsync(
        SaveDayRequest request,
        Guid callerUserId,
        bool callerCanEnterForOthers,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthorizedToSubmitFor(request.UserId, callerUserId, callerCanEnterForOthers);

        // Load all existing records for this user/location/date so we can diff within one transaction
        var existingRecords = await db
            .StatRecords.Where(r =>
                r.UserId == request.UserId
                && r.LocationId == request.LocationId
                && r.DateFrom == request.Date
                && r.DateTo == request.Date
            )
            .ToListAsync(cancellationToken);

        var incomingIds = request.Records.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToHashSet();

        // Delete records no longer present in the incoming set
        var toDelete = existingRecords.Where(r => !incomingIds.Contains(r.Id)).ToList();
        db.StatRecords.RemoveRange(toDelete);

        var results = new List<StatRecord>();

        foreach (var item in request.Records)
        {
            if (item.Id.HasValue)
            {
                // Update existing record
                var entity = existingRecords.FirstOrDefault(r => r.Id == item.Id.Value);
                if (entity is null)
                    continue; // stale ID — skip rather than error

                entity.SubCategoryMetricId = item.SubCategoryMetricId;
                entity.Value = item.Value;
                entity.Comment = item.Comment?.Trim();
                entity.Status = request.Status;
                results.Add(entity);
            }
            else
            {
                // Create new record
                var entity = new StatRecord
                {
                    DateFrom = request.Date,
                    DateTo = request.Date,
                    PeriodType = "Daily",
                    UserId = request.UserId,
                    LocationId = request.LocationId,
                    SubCategoryMetricId = item.SubCategoryMetricId,
                    Value = item.Value,
                    Comment = item.Comment?.Trim(),
                    Status = request.Status,
                };
                db.StatRecords.Add(entity);
                results.Add(entity);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return results.Adapt<List<StatRecordResponse>>();
    }

    private static void EnsureAuthorizedToSubmitFor(
        Guid? requestedUserId,
        Guid callerUserId,
        bool callerCanEnterForOthers
    )
    {
        if (!callerCanEnterForOthers && requestedUserId != callerUserId)
            throw new ForbiddenException();
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
