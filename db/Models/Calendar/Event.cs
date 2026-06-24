using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Calendar;

public class Event : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public string? Color { get; set; }

    public DateTimeOffset StartAtUtc { get; set; }

    public DateTimeOffset? EndAtUtc { get; set; }

    public DateTimeOffset? SeriesStartAtUtc { get; set; }

    public DateTimeOffset? SeriesEndAtUtc { get; set; }

    public string? TimeZoneId { get; set; }

    public bool AllDay { get; set; }

    public bool IsException { get; set; }

    public string EventTypeCode { get; set; } = CalendarEventTypeCodes.General;

    public EventType? EventType { get; set; }

    public string StatusTypeCode { get; set; } = CalendarEventStatusTypeCodes.Draft;

    public EventStatusType? StatusType { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public User? CancelledByUser { get; set; }

    public string? CancellationReason { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public int? LocationId { get; set; }

    public Location? Location { get; set; }
}
