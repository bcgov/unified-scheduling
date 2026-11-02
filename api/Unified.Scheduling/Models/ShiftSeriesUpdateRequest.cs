using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesUpdateRequest : ShiftSeriesRequest
{
    [Required]
    [JsonRequired]
    public uint ConcurrencyToken { get; init; }
}
