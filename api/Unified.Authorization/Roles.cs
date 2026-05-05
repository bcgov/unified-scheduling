namespace Unified.Authorization;

/// <summary>
/// Role name constants that match the role names stored in the <c>Roles</c> DB table
/// and configured in Keycloak.
///
/// Keep these values in sync with both the DB seed data and Keycloak role names
/// (case-sensitive). Add a corresponding DB record whenever a new constant is added here.
/// </summary>
public static class Roles
{
    public const string Administrator = nameof(Administrator);
    public const string Manager = nameof(Manager);
    public const string Scheduler = nameof(Scheduler);
    public const string Officer = nameof(Officer);
}
