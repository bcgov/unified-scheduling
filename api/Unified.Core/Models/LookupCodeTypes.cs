using System.Text.Json.Serialization;

namespace Unified.Core.Models;

/// <summary>
/// Supported lookup code types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LookupCodeTypes
{
    PositionTypes,
}
