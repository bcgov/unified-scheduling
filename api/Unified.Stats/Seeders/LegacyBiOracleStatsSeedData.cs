using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

// Explicit ranges are historical keys: never reorder demographics or reuse a retired SCM ID.
internal static class LegacyBiOracleStatsSeedData
{
    internal const int SupervisionCategoryId = 37;
    internal const int FirstSubCategoryId = 117;
    internal const int FirstMetricId = 449;
    internal const int CircuitTripsId = 509;
    internal const int SupervisionRegularId = 510;
    internal const int SupervisionOvertimeId = 511;

    private static readonly string[] Demographics =
    [
        "Male - Provincial Adult",
        "Male - Federal Adult",
        "Male - Youth",
        "Female - Provincial Adult",
        "Female - Federal Adult",
        "Female - Youth",
    ];

    internal static IEnumerable<SubCategory> CreateSubCategories()
    {
        int[] categories = [29, 27, 28];
        for (var area = 0; area < categories.Length; area++)
        {
            for (var demographic = 0; demographic < Demographics.Length; demographic++)
            {
                yield return new SubCategory
                {
                    Id = FirstSubCategoryId + area * 6 + demographic,
                    CategoryId = categories[area],
                    Name = Demographics[demographic],
                    DisplayOrder = demographic + 2,
                };
            }
        }

        yield return new SubCategory
        {
            Id = 135,
            CategoryId = SupervisionCategoryId,
            Name = "General",
            DisplayOrder = 1,
        };
    }

    internal static IEnumerable<SubCategoryMetric> CreateMetrics()
    {
        int[][] metricsByArea =
        [
            [33, 34],
            [36, 38, 40, 42],
            [37, 39, 41, 42],
        ];
        var id = FirstMetricId;
        for (var area = 0; area < metricsByArea.Length; area++)
        {
            for (var demographic = 0; demographic < Demographics.Length; demographic++)
            {
                for (var metric = 0; metric < metricsByArea[area].Length; metric++)
                {
                    yield return new SubCategoryMetric
                    {
                        Id = id++,
                        SubCategoryId = FirstSubCategoryId + area * 6 + demographic,
                        MetricId = metricsByArea[area][metric],
                        DisplayOrder = metric + 1,
                    };
                }
            }
        }

        yield return new SubCategoryMetric
        {
            Id = CircuitTripsId,
            SubCategoryId = 92,
            MetricId = 29,
            DisplayOrder = 2,
        };
        yield return new SubCategoryMetric
        {
            Id = SupervisionRegularId,
            SubCategoryId = 135,
            MetricId = 1,
            DisplayOrder = 1,
        };
        yield return new SubCategoryMetric
        {
            Id = SupervisionOvertimeId,
            SubCategoryId = 135,
            MetricId = 2,
            DisplayOrder = 2,
        };
    }
}
