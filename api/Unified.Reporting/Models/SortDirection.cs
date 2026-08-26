using System.Text.Json.Serialization;

namespace Unified.Reporting.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc,
    Desc,
}
