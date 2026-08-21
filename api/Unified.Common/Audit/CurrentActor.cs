namespace Unified.Common.Audit;

public sealed record CurrentActor(Guid? ActorUserId, string ActorName);
