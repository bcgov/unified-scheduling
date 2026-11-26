using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;

namespace Unified.Tests.Scheduling.Mappings;

public sealed class AssignmentResponseMapperTests
{
    [Fact]
    public void ToAssignmentEntryResponse_UsesAssignmentEntryAudit()
    {
        var assignment = new AssignmentEntry
        {
            Event = new Event(),
            CreatedById = Guid.NewGuid(),
            CreatedOn = new DateTimeOffset(2026, 11, 20, 17, 15, 0, TimeSpan.Zero),
            UpdatedById = Guid.NewGuid(),
            UpdatedOn = new DateTimeOffset(2026, 11, 24, 22, 30, 0, TimeSpan.Zero),
        };

        var response = AssignmentResponseMapper.ToAssignmentEntryResponse(assignment);

        Assert.Equal(assignment.CreatedById, response.CreatedById);
        Assert.Equal(assignment.CreatedOn, response.CreatedOn);
        Assert.Equal(assignment.UpdatedById, response.UpdatedById);
        Assert.Equal(assignment.UpdatedOn, response.UpdatedOn);
    }

    [Fact]
    public void ToAssignmentSeriesResponse_UsesAssignmentSeriesAudit()
    {
        var series = new AssignmentSeries
        {
            EventSeries = new EventSeries(),
            CreatedById = Guid.NewGuid(),
            CreatedOn = new DateTimeOffset(2026, 11, 20, 17, 15, 0, TimeSpan.Zero),
            UpdatedById = Guid.NewGuid(),
            UpdatedOn = new DateTimeOffset(2026, 11, 24, 22, 30, 0, TimeSpan.Zero),
        };

        var response = AssignmentResponseMapper.ToAssignmentSeriesResponse(series, []);

        Assert.Equal(series.CreatedById, response.CreatedById);
        Assert.Equal(series.CreatedOn, response.CreatedOn);
        Assert.Equal(series.UpdatedById, response.UpdatedById);
        Assert.Equal(series.UpdatedOn, response.UpdatedOn);
    }

    [Fact]
    public void ToAssignmentEntryResponse_WhenAuditIsMissing_LeavesAuditFieldsNull()
    {
        var response = AssignmentResponseMapper.ToAssignmentEntryResponse(new AssignmentEntry { Event = new Event() });

        Assert.Null(response.CreatedById);
        Assert.Null(response.CreatedOn);
        Assert.Null(response.UpdatedById);
        Assert.Null(response.UpdatedOn);
    }
}
