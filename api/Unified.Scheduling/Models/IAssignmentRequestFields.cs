namespace Unified.Scheduling.Models;

public interface IAssignmentRequestFields
{
    string Title { get; }
    DateTimeOffset StartAtUtc { get; }
    DateTimeOffset? EndAtUtc { get; }
    int AssignmentCategoryTypeId { get; }
    int AssignmentSubCategoryTypeId { get; }
    int AssignmentTypeId { get; }
    int Capacity { get; }
}
