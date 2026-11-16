using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsReconciliationWriter(UnifiedDbContext db)
{
    public async Task WriteAsync(Dictionary<string, List<SsRow>> source, SsWriteSession s, CancellationToken ct)
    {
        var parent = s.Run.MyTeamRunId ?? throw new SsReject("ReportRequiresMyTeamLineage");
        var dependencies = await db
            .DataMigrationRuns.AsNoTracking()
            .Where(x =>
                x.Source == "SS"
                && x.MyTeamRunId == parent
                && x.DryRun == s.Run.DryRun
                && (x.Job == "Training" || x.Job == "Scheduling")
                && x.Status == "Completed"
            )
            .ToListAsync(ct);
        var runIds = new[] { parent }
            .Concat(dependencies.GroupBy(x => x.Job).Select(x => x.OrderByDescending(y => y.StartedOn).First().Id))
            .ToList();
        if (runIds.Count != 3)
            throw new SsReject("IncompleteReportLineage");
        var outcomes = await db
            .DataMigrationOutcomes.AsNoTracking()
            .Where(x => runIds.Contains(x.RunId))
            .ToListAsync(ct);
        var allowed = new HashSet<string>
        {
            "Migrated",
            "Merged",
            "MigratedWithDifference",
            "Excluded",
            "DroppedBadData",
            "Rejected",
            "Failed",
            "NeedsAction",
        };
        var expected = new List<(string Type, SsRow Row)>();
        foreach (
            var table in new[]
            {
                "Region",
                "Location",
                "Role",
                "User",
                "UserRole",
                "SheriffActingRank",
                "SheriffAwayLocation",
                "SheriffLeave",
                "SheriffTraining",
                "SheriffStandardTraining",
                "LookupSortOrder",
                "Assignment",
                "Duty",
                "DutySlot",
                "Shift",
            }
        )
            expected.AddRange(source[table].Select(row => (table, row)));
        foreach (var row in source["LookupCode"])
        {
            var type = row.Int("Type") switch
            {
                0 => "CourtRoom",
                >= 1 and <= 4 => "AssignmentClassification",
                5 => "LeaveType",
                6 => "Training",
                7 => "PositionType",
                _ => null,
            };
            if (type is null)
                s.Outcome("LookupCode", row.Id, "Rejected", "UnknownSourceLookupType");
            else
                expected.Add((type, row));
        }
        foreach (var (type, row) in expected)
        {
            var ledger = outcomes.Where(x => x.EntityType == type && x.LegacyId == row.Id).ToList();
            if (ledger.Count != 1)
            {
                s.Outcome(
                    type,
                    row.Id,
                    "Failed",
                    ledger.Count == 0 ? "MissingSourceCoverage" : "DuplicateSourceCoverage"
                );
                continue;
            }
            var outcome = ledger[0];
            s.Outcome(
                type,
                row.Id,
                allowed.Contains(outcome.Outcome) ? outcome.Outcome : "Failed",
                allowed.Contains(outcome.Outcome) ? outcome.Reason : "UnknownSourceOutcome"
            );
            if (outcome.Outcome is not ("Migrated" or "Merged" or "MigratedWithDifference"))
                continue;
            var link = await db
                .DataMigrationRecords.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Source == "SS" && x.EntityType == type && x.LegacyId == row.Id, ct);
            if (link is null)
            {
                // Replace, rather than inflate, the selected row's one final outcome.
                s.Outcomes[^1].Outcome = "Failed";
                s.Outcomes[^1].Reason = "MissingPrimaryCrosswalk";
                continue;
            }
            if (
                type
                    is "User"
                        or "UserRole"
                        or "SheriffActingRank"
                        or "SheriffAwayLocation"
                        or "SheriffLeave"
                        or "SheriffTraining"
                        or "Assignment"
                        or "Shift"
                        or "Duty"
                        or "DutySlot"
                && link.SourceUpdatedAtUtc != (row.Date("UpdatedOn") ?? row.Date("CreatedOn"))
            )
            {
                s.Outcomes[^1].Outcome = "NeedsAction";
                s.Outcomes[^1].Reason = "SourceChangedSinceApplied";
                continue;
            }
            var metadata = db.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == link.TargetType);
            var keyType = metadata?.FindPrimaryKey()?.Properties.Single().ClrType;
            object? key =
                keyType == typeof(Guid) ? Guid.Parse(link.TargetKey)
                : keyType == typeof(int) ? int.Parse(link.TargetKey)
                : keyType == typeof(string) ? link.TargetKey
                : null;
            if (metadata is null || key is null || await db.FindAsync(metadata.ClrType, [key], ct) is null)
            {
                s.Outcomes[^1].Outcome = "Failed";
                s.Outcomes[^1].Reason = "MissingCrosswalkTarget";
            }
        }
        // Group metrics are deliberately not copied as selected-source outcomes.
        // Coverage and actual query checks remain separate evidence dimensions.
    }
}
