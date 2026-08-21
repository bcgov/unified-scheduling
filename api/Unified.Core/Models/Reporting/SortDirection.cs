using System.Text.Json.Serialization;

namespace Unified.Core.Models.Reporting;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc,
    Desc,
}
