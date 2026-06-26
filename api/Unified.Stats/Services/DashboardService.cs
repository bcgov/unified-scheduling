using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class DashboardService(UnifiedDbContext db) : IDashboardService
{
    public async Task<IReadOnlyCollection<DashboardEntryResponse>> GetEntriesAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        return await BuildQuery(callerHomeLocationId, queryParams)
            .OrderByDescending(r => r.DateFrom)
            .Select(r => new DashboardEntryResponse
            {
                Id = r.Id,
                UserId = r.UserId ?? Guid.Empty,
                EmployeeName = r.User != null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
                BadgeNumber = r.User != null ? r.User.BadgeNumber : null,
                Date = r.DateFrom,
                GroupId = r.SubCategoryMetric?.SubCategory?.Category?.GroupId ?? 0,
                LocationId = r.LocationId,
                WorkArea = r.SubCategoryMetric?.SubCategory?.Category?.Name ?? string.Empty,
                Subcategory = r.SubCategoryMetric?.SubCategory?.Name ?? string.Empty,
                MetricName = r.SubCategoryMetric?.Metric?.Name ?? string.Empty,
                MetricUnit = r.SubCategoryMetric?.Metric?.UnitOfMeasure ?? string.Empty,
                IsOvertime = r.SubCategoryMetric?.Metric?.IsOvertime ?? false,
                Value = r.Value,
                Status = r.Status,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseQuery = BuildQuery(callerHomeLocationId, queryParams);

        var regularHours = await baseQuery
            .Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.Metric != null
                && !r.SubCategoryMetric.Metric.IsOvertime
                && r.SubCategoryMetric.Metric.UnitOfMeasure == StatMetricUnitOfMeasure.Hours
            )
            .SumAsync(r => r.Value, cancellationToken);

        var overtimeHours = await baseQuery
            .Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.Metric != null
                && r.SubCategoryMetric.Metric.IsOvertime
                && r.SubCategoryMetric.Metric.UnitOfMeasure == StatMetricUnitOfMeasure.Hours
            )
            .SumAsync(r => r.Value, cancellationToken);

        var submittedCount = await baseQuery.CountAsync(r => r.Status == StatRecordStatus.Submitted, cancellationToken);

        var totalEntries = await baseQuery.CountAsync(cancellationToken);

        return new DashboardSummaryResponse
        {
            RegularHours = regularHours,
            OvertimeHours = overtimeHours,
            SubmittedCount = submittedCount,
            TotalEntries = totalEntries,
        };
    }

    public async Task<DashboardSignOffResponse> SignOffAsync(
        int callerHomeLocationId,
        Guid callerUserId,
        DashboardSignOffRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Load only the requested entries, scoped to the caller's location for security.
        // Only Draft or Submitted entries can be signed off.
        var toSignOff = await db
            .StatRecords.Where(r =>
                r.User != null
                && r.User.HomeLocationId == callerHomeLocationId
                && request.EntryIds.Contains(r.Id)
                && (r.Status == StatRecordStatus.Draft || r.Status == StatRecordStatus.Submitted)
            )
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var record in toSignOff)
        {
            record.Status = StatRecordStatus.SignedOff;
            record.SignedOffByUserId = callerUserId;
            record.SignedOffAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new DashboardSignOffResponse
        {
            SignedOffCount = toSignOff.Count,
            SignedOffIds = toSignOff.Select(r => r.Id).ToList(),
        };
    }

    private IQueryable<StatRecord> BuildQuery(int callerHomeLocationId, DashboardEntriesQueryParams? queryParams)
    {
        var query = db
            .StatRecords.AsNoTracking()
            .Where(r => r.User != null && r.User.HomeLocationId == callerHomeLocationId);

        if (queryParams?.EmployeeId is Guid employeeId)
            query = query.Where(r => r.UserId == employeeId);

        if (queryParams?.GroupId is int groupId)
            query = query.Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.SubCategory != null
                && r.SubCategoryMetric.SubCategory.Category != null
                && r.SubCategoryMetric.SubCategory.Category.GroupId == groupId
            );

        if (queryParams?.CategoryName is { Length: > 0 } categoryName)
            query = query.Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.SubCategory != null
                && r.SubCategoryMetric.SubCategory.Category != null
                && r.SubCategoryMetric.SubCategory.Category.Name == categoryName
            );

        if (queryParams?.SubCategoryId is int subCategoryId)
            query = query.Where(r => r.SubCategoryMetric != null && r.SubCategoryMetric.SubCategoryId == subCategoryId);

        if (queryParams?.Status is { Length: > 0 } status)
            query = query.Where(r => r.Status == status);

        if (queryParams?.FromDate is DateOnly fromDate && queryParams?.ToDate is DateOnly toDate)
            query = query.Where(r => r.DateFrom <= toDate && r.DateTo >= fromDate);
        else if (queryParams?.FromDate is DateOnly fromOnly)
            query = query.Where(r => r.DateTo >= fromOnly);
        else if (queryParams?.ToDate is DateOnly toOnly)
            query = query.Where(r => r.DateFrom <= toOnly);

        if (queryParams?.NameSearch is { Length: > 0 } search)
        {
            var pattern = $"%{search}%";
            query = query.Where(r =>
                r.User != null
                && (EF.Functions.ILike(r.User.FirstName, pattern) || EF.Functions.ILike(r.User.LastName, pattern))
            );
        }

        if (queryParams?.LocationId is int locationId)
            query = query.Where(r => r.LocationId == locationId);

        return query;
    }
}
