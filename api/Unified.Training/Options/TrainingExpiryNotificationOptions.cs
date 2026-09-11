namespace Unified.Training.Options;

public sealed class TrainingExpiryNotificationOptions
{
    public const string SectionName = "Training:ExpiryNotifications";

    public bool Enabled { get; set; } = true;

    public string CronSchedule { get; set; } = "0 7 * * *";
}
