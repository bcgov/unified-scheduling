using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IScheduleDistributionService
{
    Task<ScheduleDistributionResult> DistributeAsync(
        ScheduleDistributionRequest request,
        CancellationToken cancellationToken = default
    );
}
