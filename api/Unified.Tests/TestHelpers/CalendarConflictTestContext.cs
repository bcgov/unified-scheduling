using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Conflicts;
using Unified.Common.Calendar.Conflicts;

namespace Unified.Tests.TestHelpers;

internal static class CalendarConflictTestContext
{
    public static ClaimsPrincipal Viewer(Guid actorId, params string[] permissions) =>
        new(
            new ClaimsIdentity(
                new[] { new Claim(UnifiedClaimTypes.UserId, actorId.ToString()) }.Concat(
                    permissions.Select(permission => new Claim(UnifiedClaimTypes.Permission, permission))
                ),
                "test"
            )
        );

    public static IHttpContextAccessor Accessor(Guid actorId) =>
        new TestHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Viewer(
                    actorId,
                    nameof(Permissions.CalendarConflictsOverride),
                    nameof(Permissions.AssignmentsView),
                    nameof(Permissions.ShiftsView),
                    nameof(Permissions.LeaveView),
                    nameof(Permissions.UserTrainingsView)
                ),
            },
        };

    // Existing domain tests use synthetic participant providers. Visibility behavior is tested with real providers separately.
    public static CalendarConflictProjection Projection(Guid actorId) =>
        new([new VisibleTestProvider()], Accessor(actorId));

    public static CalendarConflictProjection Projection(IHttpContextAccessor accessor) =>
        new([new VisibleTestProvider()], accessor);

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class VisibleTestProvider : ICalendarConflictParticipantProvider
    {
        public Task<CalendarConflictParticipant?> GetParticipantAsync(
            CalendarConflictEventIdentity identity,
            Guid resourceId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<CalendarConflictParticipant?>(null);

        public Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>([]);

        public Task<CalendarConflictParticipantVisibility?> GetVisibilityAsync(
            CalendarConflictParticipant participant,
            IReadOnlySet<string> viewerPermissions,
            CancellationToken cancellationToken = default
        )
        {
            var permission = participant.SourceModule switch
            {
                "scheduling" => nameof(Permissions.AssignmentsView),
                "leave" => nameof(Permissions.LeaveView),
                "training" => nameof(Permissions.UserTrainingsView),
                "calendar" => null,
                _ => "unknown",
            };
            var visible = permission is null || viewerPermissions.Contains(permission);
            return Task.FromResult<CalendarConflictParticipantVisibility?>(
                new(visible, visible && participant.SourceModule == "scheduling" ? [participant.Identity] : [])
            );
        }
    }
}
