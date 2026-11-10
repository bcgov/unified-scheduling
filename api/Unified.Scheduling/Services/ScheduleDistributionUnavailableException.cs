namespace Unified.Scheduling.Services;

public sealed class ScheduleDistributionUnavailableException : Exception
{
    public ScheduleDistributionUnavailableException()
        : base("Schedule distribution is unavailable.") { }
}
