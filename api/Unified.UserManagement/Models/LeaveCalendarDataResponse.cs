namespace Unified.UserManagement.Models;

public sealed record LeaveCalendarDataResponse
{
    public string ModuleId { get; init; } = UserManagementConstants.SourceModule;

    public string ContributionId { get; init; } = "user-management.leave-events";

    public IReadOnlyCollection<LeaveCalendarEventResponse> Events { get; init; } = [];
}
