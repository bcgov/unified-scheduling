namespace Unified.Common.Interceptors.PostSave;

/// <summary>
/// Describes an entity save that must succeed before post-save handlers run.
/// The entity contains saved values, including generated keys. This is not a post-commit notification.
/// OccurredAtUtc identifies the application operation's time, not the transaction commit time.
/// </summary>
public sealed record SaveContext(object Entity, SaveAction Action, DateTimeOffset OccurredAtUtc);
