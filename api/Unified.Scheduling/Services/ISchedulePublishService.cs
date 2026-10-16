using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface ISchedulePublishService
{
    Task<SchedulePublishPreviewResponse> PreviewAsync(
        SchedulePublishRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SchedulePublishResult> PublishAsync(
        SchedulePublishRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default
    );
}

public sealed class SchedulePublishBlockedException(SchedulePublishPreviewResponse preview)
    : InvalidOperationException("The schedule cannot be published while blocking validation issues remain.")
{
    public SchedulePublishPreviewResponse Preview { get; } = preview;
}
