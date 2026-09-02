using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentDefinitionService
{
    Task<IReadOnlyCollection<AssignmentDefinitionResponse>> GetAssignmentDefinitionsAsync(
        int? locationId = null,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentDefinitionResponse?> GetAssignmentDefinitionByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentDefinitionResponse> CreateAssignmentDefinitionAsync(
        AssignmentDefinitionRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentDefinitionResponse?> UpdateAssignmentDefinitionAsync(
        int id,
        AssignmentDefinitionRequest request,
        CancellationToken cancellationToken = default
    );
}
