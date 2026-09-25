namespace Unified.Common.Contracts;

/// <summary>
/// Emitted after a user is created and persisted.
/// </summary>
/// <param name="UserId">The created user id.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the creation workflow occurred.</param>
public sealed record UserCreatedSignal(Guid UserId, DateTimeOffset OccurredAtUtc);
