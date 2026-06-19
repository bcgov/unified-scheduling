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
        var query = db
            .StatRecords.AsNoTracking()
            .Where(r => r.User != null && r.User.HomeLocationId == callerHomeLocationId);

        if (queryParams?.EmployeeId is Guid employeeId)
            query = query.Where(r => r.UserId == employeeId);

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

        return await query
            .OrderByDescending(r => r.DateFrom)
            .Select(r => new DashboardEntryResponse
            {
                Id = r.Id,
                UserId = r.UserId ?? Guid.Empty,
                EmployeeName = r.User != null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
                BadgeNumber = r.User != null ? r.User.BadgeNumber : null,
                Date = r.DateFrom,
                WorkArea =
                    r.SubCategoryMetric != null
                    && r.SubCategoryMetric.SubCategory != null
                    && r.SubCategoryMetric.SubCategory.Category != null
                        ? r.SubCategoryMetric.SubCategory.Category.Name
                        : string.Empty,
                Subcategory =
                    r.SubCategoryMetric != null && r.SubCategoryMetric.SubCategory != null
                        ? r.SubCategoryMetric.SubCategory.Name
                        : string.Empty,
                MetricName =
                    r.SubCategoryMetric != null && r.SubCategoryMetric.Metric != null
                        ? r.SubCategoryMetric.Metric.Name
                        : string.Empty,
                MetricUnit =
                    r.SubCategoryMetric != null && r.SubCategoryMetric.Metric != null
                        ? r.SubCategoryMetric.Metric.UnitOfMeasure
                        : string.Empty,
                IsOvertime =
                    r.SubCategoryMetric != null
                    && r.SubCategoryMetric.Metric != null
                    && r.SubCategoryMetric.Metric.IsOvertime,
                Value = r.Value,
                Status = r.Status,
            })
            .ToListAsync(cancellationToken);
    }
}
