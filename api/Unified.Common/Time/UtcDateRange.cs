namespace Unified.Common.Time;

/// <summary>A UTC half-open interval: [StartAtUtc, EndAtUtc).</summary>
public sealed record UtcDateRange(DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc);
