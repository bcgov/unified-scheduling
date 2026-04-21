using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Stats;
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
        CancellationToken cancellationToken = default
    )
    {
        var entity = MapToEntity(request);
        db.StatRecords.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<IReadOnlyCollection<StatRecordResponse>> CreateBatchAsync(
        IEnumerable<StatRecordRequest> requests,
        CancellationToken cancellationToken = default
    )
    {
        var entities = requests.Select(MapToEntity).ToList();
        db.StatRecords.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
        return entities.Adapt<List<StatRecordResponse>>();
    }

    public async Task<StatRecordResponse?> UpdateAsync(
        int id,
        StatRecordRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return null;

        entity.DateFrom = request.DateFrom;
        entity.DateTo = request.DateTo;
        entity.PeriodType = request.PeriodType.Trim();
        entity.LocationId = request.LocationId;
        entity.SubCategoryMetricId = request.SubCategoryMetricId;
        entity.Value = request.Value;
        entity.Comment = request.Comment?.Trim();
        entity.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);
        return entity.Adapt<StatRecordResponse>();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.StatRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return false;

        db.StatRecords.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static StatRecord MapToEntity(StatRecordRequest request) =>
        new()
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            PeriodType = request.PeriodType.Trim(),
            LocationId = request.LocationId,
            SubCategoryMetricId = request.SubCategoryMetricId,
            Value = request.Value,
            Comment = request.Comment?.Trim(),
            Status = request.Status,
        };
}
