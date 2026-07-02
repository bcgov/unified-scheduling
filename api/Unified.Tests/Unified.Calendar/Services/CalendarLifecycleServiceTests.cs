using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;

namespace Unified.Tests.Calendar.Services;

public sealed class CalendarLifecycleServiceTests
{
    private static readonly Guid CancelledByUserId = new("11111111-1111-1111-1111-111111111111");

    private readonly CalendarLifecycleService _service = new();

    [Fact]
    public void Publish_WhenEventWasDraft_SetsActive()
    {
        var eventEntity = new Event
        {
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            CancelledAt = DateTimeOffset.UtcNow,
            CancelledByUserId = CancelledByUserId,
            CancellationReason = "Removed",
        };

        _service.Publish(eventEntity);

        Assert.Equal(CalendarEventStatusTypeCodes.Active, eventEntity.StatusTypeCode);
        Assert.NotNull(eventEntity.CancelledAt);
        Assert.Equal(CancelledByUserId, eventEntity.CancelledByUserId);
        Assert.Equal("Removed", eventEntity.CancellationReason);
    }

    [Fact]
    public void Publish_WhenEventIsNotDraft_ThrowsInvalidOperationException()
    {
        var eventEntity = new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled };

        var exception = Assert.Throws<InvalidOperationException>(() => _service.Publish(eventEntity));

        Assert.Contains("draft status", exception.Message);
    }

    [Fact]
    public void PublishSeries_WhenChildrenHaveDifferentStatuses_PublishesOnlyDraftChildren()
    {
        var eventSeries = new EventSeries
        {
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            CancelledAt = DateTimeOffset.UtcNow,
            CancelledByUserId = CancelledByUserId,
            CancellationReason = "Removed",
        };
        var childEvents = new[]
        {
            new Event
            {
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                CancelledAt = DateTimeOffset.UtcNow,
                CancelledByUserId = CancelledByUserId,
                CancellationReason = "Draft cleanup",
            },
            new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Active },
            new Event
            {
                StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
                CancelledAt = DateTimeOffset.UtcNow,
                CancelledByUserId = CancelledByUserId,
                CancellationReason = "Removed",
            },
        };

        _service.PublishSeries(eventSeries, childEvents);

        Assert.Equal(CalendarEventStatusTypeCodes.Active, eventSeries.StatusTypeCode);
        Assert.NotNull(eventSeries.CancelledAt);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, childEvents[0].StatusTypeCode);
        Assert.NotNull(childEvents[0].CancelledAt);
        Assert.Equal(CancelledByUserId, childEvents[0].CancelledByUserId);
        Assert.Equal("Draft cleanup", childEvents[0].CancellationReason);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, childEvents[1].StatusTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, childEvents[2].StatusTypeCode);
        Assert.NotNull(childEvents[2].CancelledAt);
        Assert.Equal(CancelledByUserId, childEvents[2].CancelledByUserId);
        Assert.Equal("Removed", childEvents[2].CancellationReason);
    }

    [Fact]
    public void CancelSeries_WhenChildrenHaveDifferentStatuses_CancelsChildrenExceptAlreadyCancelled()
    {
        var cancelledAt = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var eventSeries = new EventSeries { StatusTypeCode = CalendarEventStatusTypeCodes.Active };
        var childEvents = new[]
        {
            new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Draft },
            new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Active },
            new Event
            {
                StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
                CancelledAt = cancelledAt.AddDays(-1),
                CancelledByUserId = null,
                CancellationReason = "Already closed",
            },
        };

        _service.CancelSeries(eventSeries, childEvents, cancelledAt, CancelledByUserId, "  Closed  ");

        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, eventSeries.StatusTypeCode);
        Assert.Equal(cancelledAt, eventSeries.CancelledAt);
        Assert.Equal(CancelledByUserId, eventSeries.CancelledByUserId);
        Assert.Equal("Closed", eventSeries.CancellationReason);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, childEvents[0].StatusTypeCode);
        Assert.Equal(cancelledAt, childEvents[0].CancelledAt);
        Assert.Equal(CancelledByUserId, childEvents[0].CancelledByUserId);
        Assert.Equal("Closed", childEvents[0].CancellationReason);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, childEvents[1].StatusTypeCode);
        Assert.Equal(cancelledAt, childEvents[1].CancelledAt);
        Assert.Equal(CancelledByUserId, childEvents[1].CancelledByUserId);
        Assert.Equal("Closed", childEvents[1].CancellationReason);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, childEvents[2].StatusTypeCode);
        Assert.Equal(cancelledAt.AddDays(-1), childEvents[2].CancelledAt);
        Assert.Null(childEvents[2].CancelledByUserId);
        Assert.Equal("Already closed", childEvents[2].CancellationReason);
    }

    [Fact]
    public void CanDeleteSeries_WhenSeriesIsDraftAndChildIsNotDraft_ReturnsTrue()
    {
        var eventSeries = new EventSeries { StatusTypeCode = CalendarEventStatusTypeCodes.Draft };
        var childEvents = new[]
        {
            new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Draft },
            new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Active },
        };

        var result = _service.CanDeleteSeries(eventSeries, childEvents);

        Assert.True(result);
    }

    [Fact]
    public void CanDeleteSeries_WhenSeriesIsNotDraft_ReturnsFalse()
    {
        var eventSeries = new EventSeries { StatusTypeCode = CalendarEventStatusTypeCodes.Active };
        var childEvents = new[] { new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Draft } };

        var result = _service.CanDeleteSeries(eventSeries, childEvents);

        Assert.False(result);
    }

    [Fact]
    public void ValidateNormalUpdateDoesNotChangeStatus_WhenLifecycleFieldChanges_ThrowsInvalidOperationException()
    {
        var existingEvent = new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Draft };
        var updatedEvent = new Event { StatusTypeCode = CalendarEventStatusTypeCodes.Active };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateNormalUpdateDoesNotChangeStatus(existingEvent, updatedEvent)
        );

        Assert.Contains("lifecycle fields", exception.Message);
    }

    [Fact]
    public void ValidateNormalSeriesUpdateDoesNotChangeStatus_WhenLifecycleFieldChanges_ThrowsInvalidOperationException()
    {
        var existingEventSeries = new EventSeries
        {
            StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
            CancellationReason = "Closed",
        };
        var updatedEventSeries = new EventSeries
        {
            StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled,
            CancellationReason = "Changed",
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateNormalSeriesUpdateDoesNotChangeStatus(existingEventSeries, updatedEventSeries)
        );

        Assert.Contains("lifecycle fields", exception.Message);
    }
}
