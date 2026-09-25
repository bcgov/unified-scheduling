namespace Unified.Training.Services;

public interface IUserTrainingExpiryNotificationService
{
    Task<int> SendDueExpiryNoticesAsync(CancellationToken cancellationToken = default);
}
