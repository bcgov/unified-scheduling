namespace Unified.Authorization;

/// <summary>
/// Permission name constants used throughout the application.
/// 
/// TODO: Replace this static class with a database-backed service.
/// Steps to replace:
///   1. Add a Permission entity to db/Models/UserManagement/
///   2. Add a UserRole join entity linking User → Role
///   3. Add a RolePermission join entity linking Role → Permission
///   4. Implement IPermissionService that loads from DB
///   5. Inject IPermissionService into PermissionClaimsTransformer and replace the hardcoded permissions map with DB queries
/// </summary>
public static class Permissions
{
    // --- Authentication ---
    public const string Login = nameof(Login);

    // --- User Management ---
    public const string CreateUsers = nameof(CreateUsers);
    public const string EditUsers = nameof(EditUsers);
    public const string ViewUsers = nameof(ViewUsers);
    public const string ExpireUsers = nameof(ExpireUsers);

    // --- Role Management ---
    public const string ViewRoles = nameof(ViewRoles);
    public const string CreateAndAssignRoles = nameof(CreateAndAssignRoles);
    public const string EditRoles = nameof(EditRoles);
    public const string ExpireRoles = nameof(ExpireRoles);

    // --- Scheduling ---
    public const string ViewShifts = nameof(ViewShifts);
    public const string CreateAndAssignShifts = nameof(CreateAndAssignShifts);
    public const string EditShifts = nameof(EditShifts);
    public const string ExpireShifts = nameof(ExpireShifts);
    public const string ImportShifts = nameof(ImportShifts);

    // --- Duties ---
    public const string ViewDutyRoster = nameof(ViewDutyRoster);
    public const string CreateAndAssignDuties = nameof(CreateAndAssignDuties);
    public const string EditDuties = nameof(EditDuties);
    public const string ExpireDuties = nameof(ExpireDuties);

    // --- Location / Region visibility ---
    public const string ViewHomeLocation = nameof(ViewHomeLocation);
    public const string ViewAssignedLocation = nameof(ViewAssignedLocation);
    public const string ViewRegion = nameof(ViewRegion);
    public const string ViewProvince = nameof(ViewProvince);

    // --- Reporting ---
    public const string GenerateReports = nameof(GenerateReports);
}
