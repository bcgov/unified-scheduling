using Microsoft.EntityFrameworkCore;
using Unified.Db;
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
        var query = db.StatRecords
            .AsNoTracking()
            .Where(r => r.User != null && r.User.HomeLocationId == callerHomeLocationId);

        if (queryParams?.EmployeeId is Guid employeeId)
            query = query.Where(r => r.UserId == employeeId);

        if (queryParams?.CategoryId is int categoryId)
            query = query.Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.SubCategory != null
                && r.SubCategoryMetric.SubCategory.CategoryId == categoryId);

        if (queryParams?.SubCategoryId is int subCategoryId)
            query = query.Where(r =>
                r.SubCategoryMetric != null
                && r.SubCategoryMetric.SubCategoryId == subCategoryId);

        if (queryParams?.Status is { Length: > 0 } status)
            query = query.Where(r => r.Status == status);

        if (queryParams?.FromDate is DateOnly fromDate)
            query = query.Where(r => r.DateFrom >= fromDate);

        if (queryParams?.ToDate is DateOnly toDate)
            query = query.Where(r => r.DateTo <= toDate);

        if (queryParams?.Search is { Length: > 0 } search)
        {
            var lower = search.ToLower();
            query = query.Where(r =>
                r.User != null
                && (r.User.FirstName.ToLower().Contains(lower) || r.User.LastName.ToLower().Contains(lower)));
        }

        return await query
            .OrderByDescending(r => r.DateFrom)
            .Select(r => new DashboardEntryResponse
            {
                Id = r.Id,
                UserId = r.UserId ?? Guid.Empty,
                EmployeeName = r.User != null
                    ? $"{r.User.FirstName} {r.User.LastName}".Trim()
                    : string.Empty,
                BadgeNumber = r.User != null ? r.User.BadgeNumber : null,
                Date = r.DateFrom,
                WorkArea = r.SubCategoryMetric != null
                    && r.SubCategoryMetric.SubCategory != null
                    && r.SubCategoryMetric.SubCategory.Category != null
                        ? r.SubCategoryMetric.SubCategory.Category.Name
                        : string.Empty,
                Subcategory = r.SubCategoryMetric != null && r.SubCategoryMetric.SubCategory != null
                    ? r.SubCategoryMetric.SubCategory.Name
                    : string.Empty,
                Metric = r.SubCategoryMetric != null && r.SubCategoryMetric.Metric != null
                    ? $"{r.SubCategoryMetric.Metric.Name} ({r.SubCategoryMetric.Metric.UnitOfMeasure})"
                    : string.Empty,
                Value = r.Value,
                Status = r.Status,
            })
            .ToListAsync(cancellationToken);
    }
}
