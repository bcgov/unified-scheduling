using System.Text.Json.Serialization;

namespace Unified.Common.Reporting;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc,
    Desc,
}
