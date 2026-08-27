namespace Unified.Training.Services.Reporting;

internal static class UserTrainingReportRowSorter
{
    public static IQueryable<UserTrainingReportRow> Apply(
        IQueryable<UserTrainingReportRow> rows,
        string? sortBy,
        string? sortDirection
    )
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "userdisplayname" => ApplyNameSort(rows, isDescending),
            "trainingcode" => ApplySort(rows, row => row.TrainingCode, isDescending),
            "trainingdescription" => ApplySort(rows, row => row.TrainingDescription, isDescending),
            "awardedon" => ApplySort(rows, row => row.AwardedOn, isDescending),
            "endingon" => ApplySort(rows, row => row.EndingOn, isDescending),
            "expirydate" => ApplySort(rows, row => row.ExpiryDate, isDescending),
            "noticestate" => ApplySort(rows, row => row.NoticeState, isDescending),
            _ => ApplyDefaultSort(rows),
        };
    }

    private static IQueryable<UserTrainingReportRow> ApplyNameSort(
        IQueryable<UserTrainingReportRow> rows,
        bool isDescending
    )
    {
        return isDescending
            ? rows.OrderByDescending(row => row.LastName).ThenByDescending(row => row.FirstName)
            : rows.OrderBy(row => row.LastName).ThenBy(row => row.FirstName);
    }

    private static IQueryable<UserTrainingReportRow> ApplySort<TKey>(
        IQueryable<UserTrainingReportRow> rows,
        System.Linq.Expressions.Expression<Func<UserTrainingReportRow, TKey>> keySelector,
        bool isDescending
    )
    {
        return isDescending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }

    private static IQueryable<UserTrainingReportRow> ApplyDefaultSort(IQueryable<UserTrainingReportRow> rows)
    {
        return rows.OrderBy(row => row.LastName)
            .ThenBy(row => row.FirstName)
            .ThenBy(row => row.TrainingCode)
            .ThenByDescending(row => row.AwardedOn);
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? string.Empty : sortBy.Trim().ToLowerInvariant();
    }
}
