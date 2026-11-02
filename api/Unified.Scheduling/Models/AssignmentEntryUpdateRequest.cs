using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryUpdateRequest : AssignmentEntryRequest
{
    [Required]
    [JsonRequired]
    public uint ConcurrencyToken { get; init; }

    public IReadOnlyCollection<CalendarConflictAcknowledgement>? ConflictOverrides { get; init; }
}
