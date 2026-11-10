using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;

namespace Unified.Calendar.Conflicts;

public sealed class CalendarConflictProjection(
    IEnumerable<ICalendarConflictParticipantProvider> participantProviders,
    IHttpContextAccessor? httpContextAccessor = null
)
{
    private readonly IReadOnlyCollection<ICalendarConflictParticipantProvider> _providers =
        participantProviders.ToList();
    private ClaimsPrincipal Viewer => httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();

    public bool HasPermission(string permission) => PermissionsFor(Viewer).Contains(permission);

    public Task<CalendarConflictProjectionResult> ProjectAsync(
        IReadOnlyCollection<CalendarConflict> conflicts,
        CancellationToken cancellationToken = default
    ) => ProjectAsync(conflicts, Viewer, cancellationToken);

    public async Task<CalendarConflictProjectionResult> ProjectAsync(
        IReadOnlyCollection<CalendarConflict> conflicts,
        ClaimsPrincipal viewer,
        CancellationToken cancellationToken = default
    )
    {
        var permissions = PermissionsFor(viewer);
        var details = new List<CalendarConflictResponse>();
        var warnings = new List<CalendarRestrictedConflictWarningResponse>();
        var hasRestrictedConflicts = false;
        var visibilityByParticipant =
            new Dictionary<(CalendarConflictEventIdentity, Guid), CalendarConflictParticipantVisibility>();

        async Task<CalendarConflictParticipantVisibility> VisibilityFor(CalendarConflictParticipant participant)
        {
            var key = (participant.Identity, participant.ResourceId);
            if (!visibilityByParticipant.TryGetValue(key, out var visibility))
            {
                visibility = await GetVisibilityAsync(participant, permissions, cancellationToken);
                visibilityByParticipant.Add(key, visibility);
            }
            return visibility;
        }

        foreach (var conflict in conflicts)
        {
            var first = await VisibilityFor(conflict.Entry);
            var second = await VisibilityFor(conflict.Overlaps);
            if (first.CanView && second.CanView)
            {
                details.Add(CalendarConflictResponse.FromConflict(conflict));
                continue;
            }

            // Existing overrides remain effective independently of the viewer's permissions.
            if (conflict.IsOverridden)
                continue;

            hasRestrictedConflicts = true;
            var anchors = (conflict.ReportOnEntry ? first.VisibleAnchors : []).Concat(
                conflict.ReportOnOverlaps ? second.VisibleAnchors : []
            );
            foreach (var anchor in anchors)
                warnings.Add(
                    new(
                        anchor.EventId,
                        anchor.SourceModule,
                        conflict.ResourceId,
                        "This event has a conflict with an event you do not have permission to view."
                    )
                );
        }

        return new(
            details,
            warnings.DistinctBy(warning => (warning.SourceModule, warning.EventId, warning.ResourceId)).ToList(),
            hasRestrictedConflicts
        );
    }

    public async Task<bool> CanOverrideAsync(
        CalendarConflictParticipant first,
        CalendarConflictParticipant second,
        Guid actorId,
        CancellationToken cancellationToken
    )
    {
        var permissions = PermissionsFor(Viewer);
        if (
            Viewer.TryGetCurrentUserId() != actorId
            || !permissions.Contains(nameof(Permissions.CalendarConflictsOverride))
        )
            return false;

        return (await GetVisibilityAsync(first, permissions, cancellationToken)).CanView
            && (await GetVisibilityAsync(second, permissions, cancellationToken)).CanView;
    }

    private async Task<CalendarConflictParticipantVisibility> GetVisibilityAsync(
        CalendarConflictParticipant participant,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken
    )
    {
        foreach (var provider in _providers)
        {
            var visibility = await provider.GetVisibilityAsync(participant, permissions, cancellationToken);
            if (visibility is not null)
                return visibility;
        }
        return new(false, []);
    }

    private static IReadOnlySet<string> PermissionsFor(ClaimsPrincipal viewer) =>
        viewer.FindAll(UnifiedClaimTypes.Permission).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
}

public sealed record CalendarConflictProjectionResult(
    IReadOnlyCollection<CalendarConflictResponse> Conflicts,
    IReadOnlyCollection<CalendarRestrictedConflictWarningResponse> RestrictedWarnings,
    bool HasRestrictedConflicts
);
