using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Tests.Stats.Services;

public sealed class StatRecordArchiveTests
{
    [Theory]
    [InlineData("metric", false)]
    [InlineData("subcategory", false)]
    [InlineData("category", false)]
    [InlineData("metric", true)]
    public async Task SaveDayAsync_OmittedAndSubmittedArchivedRecords_PreserveHistoryAndStillSaveActiveRecords(
        string archiveLevel,
        bool overrideSignedOff
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseInMemoryDatabase($"ArchivedStats-{Guid.NewGuid()}")
                .Options
        );
        var group = new StatGroup
        {
            Id = 3,
            Name = "Location Level",
            IsLocationLevel = true,
        };
        var category = new StatCategory
        {
            Id = 29,
            GroupId = 3,
            Group = group,
            Name = "Holding area/cellblock",
        };
        var archivedCategory = new StatCategory
        {
            Id = 32,
            GroupId = 3,
            Group = group,
            Name = "Retired",
            IsArchived = archiveLevel == "category",
        };
        var activeSub = new SubCategory
        {
            Id = 117,
            CategoryId = 29,
            Category = category,
            Name = "Male - Provincial Adult",
        };
        var oldSub = new SubCategory
        {
            Id = 106,
            CategoryId = 32,
            Category = archivedCategory,
            Name = "General",
            IsArchived = archiveLevel == "subcategory",
        };
        var activeScm = new SubCategoryMetric
        {
            Id = 449,
            SubCategoryId = 117,
            SubCategory = activeSub,
            MetricId = 33,
        };
        var oldScm = new SubCategoryMetric
        {
            Id = 418,
            SubCategoryId = 106,
            SubCategory = oldSub,
            MetricId = 56,
            IsArchived = archiveLevel == "metric",
        };
        var date = new DateOnly(2026, 10, 20);
        StatRecord Record(int id, SubCategoryMetric scm, decimal value, string status = StatRecordStatus.Draft) =>
            new()
            {
                Id = id,
                DateFrom = date,
                DateTo = date,
                PeriodType = "Daily",
                LocationId = 1,
                SubCategoryMetric = scm,
                SubCategoryMetricId = scm.Id,
                Value = value,
                Comment = "Keep original",
                PerformedAtLocationId = 5,
                Status = status,
            };
        db.StatRecords.AddRange(
            Record(1, oldScm, 37),
            Record(2, activeScm, 2),
            Record(3, activeScm, 3),
            Record(4, oldScm, 41, StatRecordStatus.SignedOff),
            Record(5, oldScm, 43)
        );
        await db.SaveChangesAsync(cancellationToken);
        var service = new StatRecordService(db, NullLogger<StatRecordService>.Instance);

        await service.SaveDayAsync(
            new SaveDayRequest
            {
                Date = date,
                LocationId = 1,
                GroupId = 3,
                Status = StatRecordStatus.Submitted,
                Records =
                [
                    new SaveDayRecordItem
                    {
                        Id = 1,
                        SubCategoryMetricId = 449,
                        Value = 999,
                        Comment = "Changed",
                        PerformedAtLocationId = 8,
                    },
                    new SaveDayRecordItem
                    {
                        Id = 2,
                        SubCategoryMetricId = 449,
                        Value = 7,
                    },
                    new SaveDayRecordItem
                    {
                        Id = 4,
                        SubCategoryMetricId = 449,
                        Value = 998,
                        Comment = "Changed signed-off",
                        PerformedAtLocationId = 8,
                    },
                ],
            },
            Guid.NewGuid(),
            false,
            overrideSignedOff,
            cancellationToken
        );

        var records = await db.StatRecords.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(cancellationToken);
        Assert.Equal(new[] { 1, 2, 4, 5 }, records.Select(r => r.Id));
        Assert.Equal(37m, records[0].Value);
        Assert.Equal("Keep original", records[0].Comment);
        Assert.Equal(418, records[0].SubCategoryMetricId);
        Assert.Equal(5, records[0].PerformedAtLocationId);
        Assert.Equal(StatRecordStatus.Submitted, records[0].Status);
        Assert.Equal(7m, records[1].Value);
        Assert.Equal(StatRecordStatus.Submitted, records[1].Status);
        Assert.Equal(41m, records[2].Value);
        Assert.Equal("Keep original", records[2].Comment);
        Assert.Equal(418, records[2].SubCategoryMetricId);
        Assert.Equal(5, records[2].PerformedAtLocationId);
        Assert.Equal(overrideSignedOff ? StatRecordStatus.Submitted : StatRecordStatus.SignedOff, records[2].Status);
        Assert.Equal(43m, records[3].Value);
        Assert.Equal(StatRecordStatus.Submitted, records[3].Status);
        Assert.Equal(
            4,
            (
                await service.GetAllAsync(new StatRecordQueryParams { LocationId = 1, GroupId = 3 }, cancellationToken)
            ).Count
        );

        // A subsequent empty save may remove active entries, but never the retired ones.
        await service.SaveDayAsync(
            new SaveDayRequest
            {
                Date = date,
                LocationId = 1,
                GroupId = 3,
                Status = StatRecordStatus.Draft,
                Records = [],
            },
            Guid.NewGuid(),
            false,
            overrideSignedOff,
            cancellationToken
        );
        Assert.Equal(
            new[] { 1, 4, 5 },
            await db.StatRecords.OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync(cancellationToken)
        );
        var archivedStatuses = await db
            .StatRecords.OrderBy(r => r.Id)
            .Select(r => r.Status)
            .ToArrayAsync(cancellationToken);
        Assert.Equal(
            new[]
            {
                StatRecordStatus.Draft,
                overrideSignedOff ? StatRecordStatus.Draft : StatRecordStatus.SignedOff,
                StatRecordStatus.Draft,
            },
            archivedStatuses
        );
    }
}
