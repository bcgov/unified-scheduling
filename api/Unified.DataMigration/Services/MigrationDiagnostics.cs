using System.Security.Cryptography;
using System.Text;
using Unified.DataMigration.Models;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// An explicit, compiled vocabulary, never learned from source/DB content.
// Unknown diagnostics retain an opaque SHA256 correlation key, not free text.
public static class MigrationDiagnostics
{
    private static readonly HashSet<string> Codes = """
        ActualRruleExpansionDoesNotMatchSource ActualSchedulingReadValidatorUnavailable
        AmbiguousCourse AmbiguousLeaveType AmbiguousLocation AmbiguousOwnedAwayUsers AmbiguousOwnedLeaveUsers
        AmbiguousOwnedShiftUsers AmbiguousPosition AmbiguousRegion AmbiguousRole AmbiguousRoom AmbiguousTrainingCategory AmbiguousUser
        AnticipatedAssignmentOmitted ApprovedOrderOmission ApprovedTestOrLeaveWaitlist AwayToLoanOutDayAvailability
        BeforeHistoryBoundary CannotRemovePrimarySourceCrosswalk CarbineCourseNotMandatory CompositionRequiresOwnedTargets
        CourseCodeShortened CourseCodeSuffixExhausted CourseDescriptionTooLong CrossTypeDefinitionNameTooLong CyclicDeleteDependencyNeedsResolution CyclicLocationParent
        DefinitionNameTooLong DefinitionWindowWidened DeletedHistory DeletedSlot DependentJobsMustRollbackFirst DescriptionTooLong
        DuplicateAgencyId DuplicateCode DuplicateCourseCode DuplicateNormalizedIdirName DuplicateSourceCoverage DuplicateSourceName
        DuplicateSsoIdentity DuplicateTrimmedLeaveCode DutyMultiEntryRequiresGraphRepartition DutySplitByDistinctSheriff
        EmptyInterval EmptySlotInterval EntityOwnershipViolation ExcusedSheriff ExistingCrosswalk ExistingDefinitionCollision
        ExistingDutySlotReconciliationRequired ExistingGroupCrosswalk ExistingUserSsoIdentityMismatch
        ExpiredUnreferencedDefinition ExpiredUnreferencedRoom FlagOnlySegmentBesideStaffDropped FutureAssignmentUniquenessConflict
        FutureGapRequiresSourceRepair FutureShiftConflict G10DraftSeriesCutoverActiveStandalone GraphMissingFullSourceMembership
        GraphProposalRequiresAction GraphReplacementRequiresInverseJournal GraphRequiresOriginalOwnedTarget GraphSealRequiresNewOwnedEntry
        GraphUnresolvedCrosswalk GraphUnresolvedLiveSlot GroupMergeRequiresMultiTargetRepartition GroupSplitRequiresMultiTargetRepartition
        GroupWriteFailed HistoricalGapBlocksSeparate IncompleteReportLineage InconsistentSeriesMember
        IndependentO1ToO5CaptureComparisonRequired InvalidAdvanceNotice InvalidCode InvalidCourseCode InvalidGender InvalidLeaveCode
        InvalidRoomCode InvalidTimezone LateOlderHistoryAppended LaterSameJobRunMustRollbackFirst LinkedOnlyTargetCannotUpdate
        MaterialMissingShiftUserLink MissingActiveCarbineProfile MissingAgencyId MissingAssignment MissingAssignmentCode
        MissingAssignmentLocation MissingAssignmentLookup MissingAwayLocation MissingCrosswalkTarget MissingDuty MissingDutyLocation
        MissingEffectiveDate MissingEnd MissingLeaveType MissingName MissingOrInconsistentStandardLeave MissingOriginalRollbackJournal
        MissingOriginalTargetVersionProof MissingParent MissingPrimaryCrosswalk MissingRank MissingRole MissingRoleName MissingRoomCode
        MissingShiftLocation MissingSourceAssignment MissingSourceCoverage MissingSourceUser MissingStaffPermissionSnapshot
        MissingStandardRole MissingStart MissingStatsClassification MissingStatusTimezoneFallback MissingTraining MissingUnifiedSystemUser
        MissingUser MixedShiftTimezones NegativeTrainingPeriod NoOverlappingShiftUserLinkDropped NullIdentityTextToEmpty OrderOverflow
        OriginalBeforeImageUnavailable OriginalRollbackKeyRequired OwnedFullGroupUpdate OwnedMappedUpdate OwnedTargetDrift OwnedUpdateRequiresReview
        ParentDutyNotMigrated PreviousPrivatePhotoCannotBeRestored PreviouslyMigratedNowExcludedRetained ReportMustRollbackFirst
        ReportRequiresMyTeamLineage RestoredRowAlsoDependsOnDeletedTarget RetainedLeaveTargetCollision ReversedDates RunNotApplied
        RollbackOnlyCandidatesNotAppliedCrosswalks ScopedLeaveType SeriesCannotChangeLinkedOnlyEntry SeriesEntryDriftOrExistingMembership
        SimulationCandidateReceiptMismatch SimulationExplicitSchemaUnsupported SimulationOnlyNotAppliedLineage SimulationParentRehydrationFailed
        SimulationUnkeyedTableUnsupported SlotFlagsRepresentedOnEntry SlotPartlyOutsideShift SourceChangedSinceApplied SourceJobRunning
        SsoIdentityCollision StaleSimulationInputsOrTargetBaseline StandaloneInsufficientPatternOccurrences StandaloneMissingRruleOccurrence
        StandaloneSplitDuty StandaloneUnsupportedPattern StatsAssumptionC1S3 TargetTrainingAllDayNotInSource TargetTrainingTimezoneUnavailable
        TrainingCannotChangeMyTeamMappedFields UnapprovedTrainingCategory UnexpectedNonOtherComment UnifiedMandatoryPolicyReplacesFutureRules
        UnifiedSystemUser UnkeyedDependentTableRequiresExplicitResolution UnknownEntityOwnership UnknownLeaveType UnknownSourceLookupType
        UnknownSourceOutcome UnknownTargetType UnownedGraphDependent UnresolvedPrimaryRetryItems UnresolvedRank UnsupportedScopedCourse
        UnsupportedSimulationJob UnusedClassificationCode UserIdentityCollision VersionOverflow WrongOrMissingSimulationCandidateNamespace
        WrongOrMissingSimulationParentLineage WrongSimulationNamespace
        DistinctPrivateCreatedTargetsMatchCounter PrivateTargetCounterMismatch NoDanglingPrivateSchedulingCrosswalks DanglingPrivateSchedulingCrosswalk
        ActualPublishPreviewBlocked ActualCalendarReadAndPublishPreviewExecuted ActualIndependentOracleRowsMatched
        ActualIndependentOracleDifferencesOrUnsupportedSurfaces SsComparisonPendingFutureTargetOutsideIntervalSuppression
        SsComparisonPendingHistoricalTargetOutsideIntervalSuppression StandaloneInconsistentMember
        Canceled ExecutionFailed RollbackFailed QueuedExecutionRefused DispatchFailed
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> Entities = """
        Region Location Role User UserRole PositionType LeaveType LookupSortOrder SheriffActingRank SheriffAwayLocation SheriffLeave
        Training SheriffTraining SheriffStandardTraining TrainingCategory CourtRoom AssignmentClassification Assignment Duty DutySlot Shift
        AssignmentSeries DutyEntry SchedulingComponent SchedulingMember ExcludedTableMetric RetryQueueValidation SourceOracleValidation
        SimulationNamespace SimulationValidation SchedulingValidation SchedulingReadValidation AwayAvailabilityValidation
        AdverseCounterValidation AdverseGraphValidation BusinessQueryValidation Operations
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> Outcomes = """
        Migrated Merged MigratedWithDifference Excluded DroppedBadData NeedsAction Rejected Failed Unchanged
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    public static string Opaque(string? text) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? "")));

    public static string Key(string? text) =>
        text is { Length: > 0 and <= 128 } && (Guid.TryParseExact(text, "D", out _) || text.All(char.IsAsciiDigit))
            ? text
            : Opaque(text);

    public static string Code(string? text) =>
        text is null ? ""
        : Codes.Contains(text) ? text
        : Opaque(text);

    public static string Entity(string? text) => text is not null && Entities.Contains(text) ? text : Opaque(text);

    public static MigrationFindingDto Finding(DataMigrationOutcome value)
    {
        var excludedMetric =
            value.EntityType == "ExcludedTableMetric"
            && value.LegacyId
                is "Audit"
                    or "JcSynchronization"
                    or "DataProtectionKeys"
                    or "Permission"
                    or "RolePermission";
        const string prefix = "ExcludedSystemOrPermissionRows:";
        var countedExclusion =
            excludedMetric
            && value.Reason is not null
            && value.Reason.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(value.Reason[prefix.Length..], out var count)
            && count >= 0;
        return new(
            value.Id,
            "Outcome",
            Entity(value.EntityType),
            excludedMetric ? value.LegacyId : Key(value.LegacyId),
            Outcomes.Contains(value.Outcome) ? value.Outcome : "NeedsAction",
            countedExclusion ? value.Reason! : Code(value.Reason),
            null
        );
    }

    public static MigrationFindingDto Failure(DataMigrationFailure value, bool report = false) =>
        new(
            value.Id,
            "Failure",
            Entity(value.EntityType),
            Key(value.LegacyId),
            report && MigrationTechnicalFailure.IsReportDiagnostic(value.ErrorType) ? "NeedsAction" : "Failed",
            Code(value.ErrorType),
            value.OccurredOn
        );
}
