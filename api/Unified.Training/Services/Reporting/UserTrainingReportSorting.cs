using Unified.Core.Models.Reporting;

namespace Unified.Training.Services.Reporting;

internal static class UserTrainingReportSorting
{
    private delegate IOrderedEnumerable<UserTrainingReportRow> Sorter(IEnumerable<UserTrainingReportRow> rows);

    private static readonly IReadOnlyDictionary<string, (Sorter Asc, Sorter Desc)> Sorters =
        new Dictionary<string, (Sorter Asc, Sorter Desc)>
        {
            ["userdisplayname"] = (
                rows => rows.OrderBy(row => row.LastName).ThenBy(row => row.FirstName),
                rows => rows.OrderByDescending(row => row.LastName).ThenByDescending(row => row.FirstName)
            ),
            ["trainingcode"] = (
                rows => rows.OrderBy(row => row.TrainingCode),
                rows => rows.OrderByDescending(row => row.TrainingCode)
            ),
            ["trainingdescription"] = (
                rows => rows.OrderBy(row => row.TrainingDescription),
                rows => rows.OrderByDescending(row => row.TrainingDescription)
            ),
            ["trainingcategory"] = (
                rows => rows.OrderBy(row => row.TrainingCategory),
                rows => rows.OrderByDescending(row => row.TrainingCategory)
            ),
            ["awardedon"] = (
                rows => rows.OrderBy(row => row.AwardedOn),
                rows => rows.OrderByDescending(row => row.AwardedOn)
            ),
            ["endingon"] = (
                rows => rows.OrderBy(row => row.EndingOn),
                rows => rows.OrderByDescending(row => row.EndingOn)
            ),
            ["expirydate"] = (
                rows => rows.OrderBy(row => row.ExpiryDate),
                rows => rows.OrderByDescending(row => row.ExpiryDate)
            ),
            ["version"] = (
                rows => rows.OrderBy(row => row.Version),
                rows => rows.OrderByDescending(row => row.Version)
            ),
            ["noticestate"] = (
                rows => rows.OrderBy(row => row.NoticeState),
                rows => rows.OrderByDescending(row => row.NoticeState)
            ),
        };

    public static List<UserTrainingReportRow> Apply(
        IEnumerable<UserTrainingReportRow> rows,
        string? sortBy,
        SortDirection sortDirection
    )
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (Sorters.TryGetValue(normalizedSortBy, out var sorter))
        {
            return (sortDirection == SortDirection.Desc ? sorter.Desc(rows) : sorter.Asc(rows)).ToList();
        }

        return rows
            .OrderBy(row => row.LastName)
            .ThenBy(row => row.FirstName)
            .ThenBy(row => row.TrainingCode)
            .ThenByDescending(row => row.AwardedOn)
            .ToList();
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? string.Empty : sortBy.Trim().ToLowerInvariant();
    }
}
