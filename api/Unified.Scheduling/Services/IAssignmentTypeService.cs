using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentTypeService
{
    Task<IReadOnlyCollection<AssignmentTypeResponse>> GetAssignmentTypesAsync(
        CancellationToken cancellationToken = default
    );

    Task<AssignmentTypeResponse?> GetAssignmentTypeByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentTypeResponse?> GetAssignmentTypeByCodeAsync(
        string code,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentTypeResponse> CreateAssignmentTypeAsync(
        AssignmentTypeRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentTypeResponse?> UpdateAssignmentTypeAsync(
        int id,
        AssignmentTypeRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentTypeResponse?> ExpireAssignmentTypeAsync(int id, CancellationToken cancellationToken = default);
}
