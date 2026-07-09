using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentTypeService
{
    Task<IReadOnlyCollection<AssignmentTypeResponse>> GetAssignmentTypesAsync(
        int? locationId = null,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentTypeResponse?> GetAssignmentTypeByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentTypeResponse?> GetAssignmentTypeByCodeAsync(
        string code,
        int locationId,
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
