namespace Unified.Authorization;

/// <summary>
/// Role name constants that match the role names configured in Keycloak.
///
/// TODO: Replace with database-backed role names once the Role entity and
/// its Keycloak sync are implemented. Ensure Keycloak role names remain
/// in sync with the values here when migrating.
/// </summary>
public static class Roles
{
    public const string Administrator = nameof(Administrator);
    public const string Manager = nameof(Manager);
    public const string Scheduler = nameof(Scheduler);
    public const string Officer = nameof(Officer);
}
