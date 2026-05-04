using System.ComponentModel.DataAnnotations;
using Mapster;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.UserManagement;

/// <summary>
/// Represents a named permission in the system.
///
/// Design notes:
///   - Id is the string permission name constant (e.g. "ShiftsEdit") and serves as the primary key.
///   - Description is the human-readable description of what the permission grants.
///   - Permissions are managed exclusively via seed data; there is no create endpoint.
///
/// Data migration mapping from Sheriff Scheduling (SS) Permission table:
///   SS Permission.Name  →  Unified Permission.Id
///   SS Permission.Description  →  Unified Permission.Description
///
///   SS name → Unified Id mapping:
///   Login                    → AuthLogin
///   CreateUsers              → UsersCreate
///   ExpireUsers              → UsersExpire
///   EditUsers                → UsersEdit
///   ViewRoles                → RolesView
///   CreateAndAssignRoles     → RolesCreateAndAssign
///   ExpireRoles              → RolesExpire
///   EditRoles                → RolesEdit
///   CreateTypes              → TypesCreate
///   EditTypes                → TypesEdit
///   ExpireTypes              → TypesExpire
///   ViewShifts               → ShiftsView
///   CreateAndAssignShifts    → ShiftsCreateAndAssign
///   ExpireShifts             → ShiftsExpire
///   EditShifts               → ShiftsEdit
///   ImportShifts             → ShiftsImport
///   ViewDistributeSchedule   → ScheduleViewDistribute
///   ViewHomeLocation         → LocationViewHome
///   ViewAssignedLocation     → LocationViewAssigned
///   ViewRegion               → LocationViewRegion
///   ViewProvince             → LocationViewProvince
///   ExpireLocation           → LocationExpire
///   CreateAssignments        → AssignmentsCreate
///   EditAssignments          → AssignmentsEdit
///   ExpireAssignments        → AssignmentsExpire
///   ViewDutyRoster           → DutyRosterView
///   CreateAndAssignDuties    → DutiesCreateAndAssign
///   EditDuties               → DutiesEdit
///   ExpireDuties             → DutiesExpire
///   EditIdir                 → IdirEdit
///   EditPastTraining         → TrainingEditPast
///   RemovePastTraining       → TrainingRemovePast
///   ViewDutyRosterInFuture   → DutyRosterViewFuture
///   ViewAllFutureShifts      → ShiftsViewAllFuture
///   ViewOtherProfiles        → UsersViewOtherProfiles
///   GenerateReports          → ReportsGenerate
///   AdjustTrainingExpiry     → TrainingAdjustExpiry
///   ExemptFromTraining       → TrainingExempt
/// </summary>
[AdaptTo("[name]Dto")]
public class Permission : BaseEntity
{
    /// <summary>
    /// The permission name constant, used as the primary key (e.g. "ShiftsEdit").
    /// Corresponds to the Name field in the Sheriff Scheduling Permission table.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what this permission grants.
    /// Corresponds to the Description field in the Sheriff Scheduling Permission table.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
