using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IWorkingHoursService
{
    Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(
        WorkingHoursQuery query,
        CancellationToken cancellationToken = default);
}