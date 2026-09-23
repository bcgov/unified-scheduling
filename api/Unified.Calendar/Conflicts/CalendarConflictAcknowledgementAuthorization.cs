using System.Security.Claims;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public static class CalendarConflictAcknowledgementAuthorization
{
    public static bool TryResolveActor(
        ClaimsPrincipal user,
        IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements,
        out Guid? actorId
    )
    {
        actorId = null;
        if (acknowledgements is not { Count: > 0 })
            return true;
        if (!user.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.CalendarConflictsOverride)))
            return false;

        actorId = user.TryGetCurrentUserId();
        return actorId.HasValue;
    }
}
