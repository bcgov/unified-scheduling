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
    public const string AuthLogin = nameof(AuthLogin);

    // --- Users ---
    public const string UsersCreate = nameof(UsersCreate);
    public const string UsersEdit = nameof(UsersEdit);
    public const string UsersView = nameof(UsersView);
    public const string UsersExpire = nameof(UsersExpire);
    public const string UsersViewOtherProfiles = nameof(UsersViewOtherProfiles);

    // --- Roles ---
    public const string RolesView = nameof(RolesView);
    public const string RolesCreateAndAssign = nameof(RolesCreateAndAssign);
    public const string RolesEdit = nameof(RolesEdit);
    public const string RolesExpire = nameof(RolesExpire);

    // --- Types ---
    public const string TypesCreate = nameof(TypesCreate);
    public const string TypesEdit = nameof(TypesEdit);
    public const string TypesExpire = nameof(TypesExpire);

    // --- Shifts ---
    public const string ShiftsView = nameof(ShiftsView);
    public const string ShiftsCreateAndAssign = nameof(ShiftsCreateAndAssign);
    public const string ShiftsEdit = nameof(ShiftsEdit);
    public const string ShiftsExpire = nameof(ShiftsExpire);
    public const string ShiftsImport = nameof(ShiftsImport);
    public const string ShiftsViewAllFuture = nameof(ShiftsViewAllFuture);

    // --- Schedule ---
    public const string ScheduleViewDistribute = nameof(ScheduleViewDistribute);

    // --- Assignments ---
    public const string AssignmentsCreate = nameof(AssignmentsCreate);
    public const string AssignmentsEdit = nameof(AssignmentsEdit);
    public const string AssignmentsExpire = nameof(AssignmentsExpire);

    // --- Duty Roster ---
    public const string DutyRosterView = nameof(DutyRosterView);
    public const string DutyRosterViewFuture = nameof(DutyRosterViewFuture);

    // --- Duties ---
    public const string DutiesCreateAndAssign = nameof(DutiesCreateAndAssign);
    public const string DutiesEdit = nameof(DutiesEdit);
    public const string DutiesExpire = nameof(DutiesExpire);

    // --- Location ---
    public const string LocationViewHome = nameof(LocationViewHome);
    public const string LocationViewAssigned = nameof(LocationViewAssigned);
    public const string LocationViewRegion = nameof(LocationViewRegion);
    public const string LocationViewProvince = nameof(LocationViewProvince);
    public const string LocationExpire = nameof(LocationExpire);

    // --- Training ---
    public const string TrainingEditPast = nameof(TrainingEditPast);
    public const string TrainingRemovePast = nameof(TrainingRemovePast);
    public const string TrainingAdjustExpiry = nameof(TrainingAdjustExpiry);
    public const string TrainingExempt = nameof(TrainingExempt);

    // --- IDIR ---
    public const string IdirEdit = nameof(IdirEdit);

    // --- Reports ---
    public const string ReportsGenerate = nameof(ReportsGenerate);
}
