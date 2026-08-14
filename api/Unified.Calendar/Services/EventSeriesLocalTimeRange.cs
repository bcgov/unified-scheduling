namespace Unified.Calendar.Services;

public sealed record EventSeriesLocalTimeRange(DateTime StartLocal, DateTime? EndLocal, TimeSpan? Duration);
