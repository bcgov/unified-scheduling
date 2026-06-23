using System.Text;

namespace Unified.Common.Logging;

public static class LogSanitizer
{
    private const int DefaultMaxLength = 128;

    public static string? UserText(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var builder = new StringBuilder(capacity: Math.Min(trimmed.Length, maxLength) + 1);

        foreach (var ch in trimmed)
        {
            if (builder.Length >= maxLength)
            {
                builder.Append("...");
                break;
            }

            builder.Append(char.IsControl(ch) ? ' ' : ch);
        }

        return builder.ToString();
    }

    public static bool HasValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static int? Length(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length;
    }
}
