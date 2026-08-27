namespace Unified.Common.Reporting;

public abstract class ReportQueryHandlerBase
{
    protected static string? ParseStringFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        if (!filters.TryGetValue(filterKey, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    protected static T? ParseFilter<T>(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey,
        TryParseFilterValue<T> tryParse,
        string formatMessage
    )
        where T : struct
    {
        var rawValue = ParseStringFilter(filters, filterKey);
        if (rawValue is null)
        {
            return null;
        }

        if (tryParse(rawValue, out var parsedValue))
        {
            return parsedValue;
        }

        throw new ArgumentException($"Filter '{filterKey}' {formatMessage}.");
    }

    protected delegate bool TryParseFilterValue<T>(string rawValue, out T parsedValue);
}