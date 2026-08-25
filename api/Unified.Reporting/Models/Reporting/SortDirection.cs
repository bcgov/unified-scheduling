using System.Text.Json.Serialization;

namespace Unified.Reporting.Models.Reporting;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc,
    Desc,
}