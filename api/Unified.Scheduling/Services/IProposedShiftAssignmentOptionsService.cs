using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IProposedShiftAssignmentOptionsService
{
    Task<ProposedShiftAssignmentOptionsResponse> GetOptionsAsync(
        ProposedShiftAssignmentOptionsRequest request,
        CancellationToken cancellationToken = default
    );
}