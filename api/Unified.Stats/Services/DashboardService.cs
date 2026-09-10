using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Logging;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class DashboardService(UnifiedDbContext db, ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<IReadOnlyCollection<DashboardEntryResponse>> GetEntriesAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving dashboard entries for location {LocationId}", callerHomeLocationId);
        // Note: null-conditional operators (?.) are not allowed inside expression tree lambdas
        // (CS8072 — a C# compiler restriction, not an EF Core one), so navigation checks use
        // explicit != null comparisons instead.
        var isLocationLevel = await IsLocationLevelFilterAsync(queryParams, cancellationToken);
        return await BuildQuery(callerHomeLocationId, queryParams, isLocationLevel)
            .OrderByDescending(r => r.DateFrom)
            .Select(r => new DashboardEntryResponse
            {
                Id = r.Id,
                UserId = r.UserId ?? Guid.Empty,
                EmployeeName = r.User != null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
                BadgeNumber = r.User != null ? r.User.BadgeNumber : null,
                Date = r.DateFrom,
                GroupId =
                    r.SubCategoryMetric != null
                    && r.SubCategoryMetric.SubCategory != null
                    && r.SubCategoryMetric.SubCategory.Category != null
                        ? r.SubCategoryMetric.SubCategory.Category.GroupId
                        : 0,
                LocationId = r.LocationId,
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

    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving dashboard summary for location {LocationId}", callerHomeLocationId);

        var isLocationLevel = await IsLocationLevelFilterAsync(queryParams, cancellationToken);
        var baseQuery = BuildQuery(callerHomeLocationId, queryParams, isLocationLevel);

        var summaryTask = baseQuery
            .Select(r => new
            {
                IsRegular =
                    r.SubCategoryMetric != null
                    && r.SubCategoryMetric.Metric != null
                    && !r.SubCategoryMetric.Metric.IsOvertime
                    && r.SubCategoryMetric.Metric.UnitOfMeasure == StatMetricUnitOfMeasure.Hours,
                IsOvertime =
                    r.SubCategoryMetric != null
                    && r.SubCategoryMetric.Metric != null
                    && r.SubCategoryMetric.Metric.IsOvertime
                    && r.SubCategoryMetric.Metric.UnitOfMeasure == StatMetricUnitOfMeasure.Hours,
                IsSubmitted = r.Status == StatRecordStatus.Submitted,
                r.Value,
            })
            .ToListAsync(cancellationToken);

        var rows = await summaryTask;

        return new DashboardSummaryResponse
        {
            RegularHours = rows.Where(r => r.IsRegular).Sum(r => r.Value ?? 0),
            OvertimeHours = rows.Where(r => r.IsOvertime).Sum(r => r.Value ?? 0),
            SubmittedCount = rows.Count(r => r.IsSubmitted),
            TotalEntries = rows.Count,
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
                (
                    (r.User != null && r.User.HomeLocationId == callerHomeLocationId)
                    || (r.UserId == null && r.LocationId == callerHomeLocationId)
                )
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

    private async Task<bool> IsLocationLevelFilterAsync(
        DashboardEntriesQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        if (queryParams?.GroupId is not int gid) return false;
        return await db.StatGroups.AnyAsync(g => g.Id == gid && g.IsLocationLevel, cancellationToken);
    }

    private IQueryable<StatRecord> BuildQuery(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams,
        bool isLocationLevelGroup
    )
    {
        logger.LogDebug(
            "Building dashboard query for location {LocationId}, employee {EmployeeId}, category provided {HasCategory}, name search provided {HasNameSearch}, name search length {NameSearchLength}, status {Status}",
            callerHomeLocationId,
            queryParams?.EmployeeId,
            LogSanitizer.HasValue(queryParams?.CategoryName),
            LogSanitizer.HasValue(queryParams?.NameSearch),
            LogSanitizer.Length(queryParams?.NameSearch),
            queryParams?.Status
        );

        IQueryable<StatRecord> query;
        if (isLocationLevelGroup)
        {
            query = db
                .StatRecords.AsNoTracking()
                .Where(r => r.UserId == null && r.LocationId == callerHomeLocationId);
        }
        else
        {
            // Include employee records scoped to the caller's home location, plus
            // location-level records (UserId is null) for the same location.
            query = db
                .StatRecords.AsNoTracking()
                .Where(r =>
                    (r.User != null && r.User.HomeLocationId == callerHomeLocationId)
                    || (r.UserId == null && r.LocationId == callerHomeLocationId)
                );
        }

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
