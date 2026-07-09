using System.Text.Json.Serialization;

namespace Unified.Authorization;

/// <summary>
/// Permission enum for all application permissions.
/// Use these permission values to control access throughout the application.
/// Can be referenced in both backend and frontend code.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Permissions
{
    // --- Authentication ---
    AuthLogin,

    // --- Users ---
    UsersCreate,
    UsersEdit,
    UserRoleAssign,
    UsersView,
    UsersExpire,
    UsersViewOtherProfiles,

    // --- Roles ---
    RolesView,
    RolesCreate,
    RolesEdit,
    RolesExpire,

    // --- Types ---
    TypesCreate,
    TypesEdit,
    TypesExpire,

    // --- Shifts ---
    ShiftsView,
    ShiftsCreateAndAssign,
    ShiftsEdit,
    ShiftsExpire,

    // --- Schedule ---
    ScheduleViewDistribute,

    // --- Assignments ---
    AssignmentsView,
    AssignmentsCreate,
    AssignmentsAssign,
    AssignmentsEdit,
    AssignmentsExpire,
    AssignmentTypeRead,
    AssignmentTypeWrite,
    AssignmentTypeExpire,

    // --- Duty Roster ---
    DutyRosterView,
    DutyRosterViewFuture,

    // --- Duties ---
    DutiesCreateAndAssign,
    DutiesEdit,
    DutiesExpire,

    // --- Location ---
    LocationViewHome,
    LocationViewAssigned,
    LocationViewRegion,
    LocationViewProvince,
    LocationExpire,

    // --- Training ---
    TrainingEditPast,
    TrainingRemovePast,
    TrainingAdjustExpiry,
    TrainingExempt,

    // --- Acting Positions ---
    ActingPositionsView,
    ActingPositionsCreate,
    ActingPositionsEdit,
    ActingPositionsExpire,

    // --- IDIR ---
    IdirEdit,

    // --- Reports ---
    ReportsGenerate,

    // --- Stats ---
    StatsRecordsEnterForOthers,

    // --- Dashboard ---
    DashboardView,
    DashboardSignOff,
    DashboardSubmit,
}
