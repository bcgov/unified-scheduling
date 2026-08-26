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

    protected static T? ParseOptional<T>(
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

        if (tryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Filter '{filterKey}' {formatMessage}.");
    }

    protected static bool TryParsePositiveInt(string rawValue, out int parsed)
    {
        var success = int.TryParse(rawValue, out parsed) && parsed > 0;

        if (!success)
        {
            parsed = default;
        }

        return success;
    }

    protected static string? NormalizeForContains(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    protected delegate bool TryParseFilterValue<T>(string rawValue, out T parsed);
}