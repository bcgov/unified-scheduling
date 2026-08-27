namespace Unified.Training.Services.Reporting;

internal static class UserTrainingReportRowSorter
{
    public static List<UserTrainingReportRow> Apply(
        IEnumerable<UserTrainingReportRow> rows,
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

    private static List<UserTrainingReportRow> ApplyNameSort(IEnumerable<UserTrainingReportRow> rows, bool isDescending)
    {
        return
        [
            .. (
                isDescending
                    ? rows.OrderByDescending(row => row.LastName).ThenByDescending(row => row.FirstName)
                    : rows.OrderBy(row => row.LastName).ThenBy(row => row.FirstName)
            ),
        ];
    }

    private static List<UserTrainingReportRow> ApplySort<TKey>(
        IEnumerable<UserTrainingReportRow> rows,
        Func<UserTrainingReportRow, TKey> keySelector,
        bool isDescending
    )
    {
        return (isDescending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector)).ToList();
    }

    private static List<UserTrainingReportRow> ApplyDefaultSort(IEnumerable<UserTrainingReportRow> rows)
    {
        return
        [
            .. rows.OrderBy(row => row.LastName)
                .ThenBy(row => row.FirstName)
                .ThenBy(row => row.TrainingCode)
                .ThenByDescending(row => row.AwardedOn),
        ];
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? string.Empty : sortBy.Trim().ToLowerInvariant();
    }
}
