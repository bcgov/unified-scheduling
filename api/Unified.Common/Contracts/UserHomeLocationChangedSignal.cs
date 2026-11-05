namespace Unified.Common.Contracts;

/// <summary>
/// Emitted after a changed user home location is persisted, within the update transaction.
/// </summary>
/// <param name="UserId">The updated user id.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the home-location update occurred.</param>
public sealed record UserHomeLocationChangedSignal(Guid UserId, DateTimeOffset OccurredAtUtc);
