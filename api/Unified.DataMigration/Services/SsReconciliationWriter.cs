using System.Reflection;
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
        // Retain every member: duplicate coverage across lineage runs must still fail.
        var outcomeIndex = outcomes.ToLookup(x => (x.EntityType, x.LegacyId));
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
        var applied = expected
            .Where(x =>
            {
                var ledger = outcomeIndex[(x.Type, x.Row.Id)].ToArray();
                return ledger.Length == 1 && ledger[0].Outcome is "Migrated" or "Merged" or "MigratedWithDifference";
            })
            .ToList();
        var links = new List<DataMigrationRecord>();
        foreach (var entity in applied.GroupBy(x => x.Type))
        foreach (var ids in entity.Select(x => x.Row.Id).Distinct().Chunk(1000))
            links.AddRange(
                await db
                    .DataMigrationRecords.AsNoTracking()
                    .Where(x => x.Source == "SS" && x.EntityType == entity.Key && ids.Contains(x.LegacyId))
                    .ToListAsync(ct)
            );
        var linkIndex = links.ToDictionary(x => (x.EntityType, x.LegacyId));
        var targets = new HashSet<(string Type, string Key)>();
        var currentLinks = applied
            .Where(x => linkIndex.TryGetValue((x.Type, x.Row.Id), out var link) && !SourceChanged(x.Type, x.Row, link))
            .Select(x => linkIndex[(x.Type, x.Row.Id)])
            .Distinct()
            .GroupBy(x => x.TargetType);
        foreach (var entity in currentLinks)
        {
            var metadata = db.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == entity.Key);
            var key = metadata?.FindPrimaryKey()?.Properties.Single();
            if (
                key is null
                || key.ClrType != typeof(Guid) && key.ClrType != typeof(int) && key.ClrType != typeof(string)
            )
                continue;
            var read = typeof(SsReconciliationWriter)
                .GetMethod(nameof(ReadTargetKeysAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(metadata!.ClrType, key.ClrType);
            var found = await (Task<HashSet<string>>)
                read.Invoke(this, [key.Name, entity.Select(x => x.TargetKey).Distinct().ToArray(), ct])!;
            targets.UnionWith(found.Select(x => (entity.Key, x)));
        }
        foreach (var (type, row) in expected)
        {
            var ledger = outcomeIndex[(type, row.Id)].ToArray();
            if (ledger.Length != 1)
            {
                s.Outcome(
                    type,
                    row.Id,
                    "Failed",
                    ledger.Length == 0 ? "MissingSourceCoverage" : "DuplicateSourceCoverage"
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
            if (!linkIndex.TryGetValue((type, row.Id), out var link))
            {
                // Replace, rather than inflate, the selected row's one final outcome.
                s.Outcomes[^1].Outcome = "Failed";
                s.Outcomes[^1].Reason = "MissingPrimaryCrosswalk";
                continue;
            }
            if (SourceChanged(type, row, link))
            {
                s.Outcomes[^1].Outcome = "NeedsAction";
                s.Outcomes[^1].Reason = "SourceChangedSinceApplied";
                continue;
            }
            if (!targets.Contains((link.TargetType, link.TargetKey)))
            {
                s.Outcomes[^1].Outcome = "Failed";
                s.Outcomes[^1].Reason = "MissingCrosswalkTarget";
            }
        }
        // Group metrics are deliberately not copied as selected-source outcomes.
        // Coverage and actual query checks remain separate evidence dimensions.
    }

    private static bool SourceChanged(string type, SsRow row, DataMigrationRecord link) =>
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
        && link.SourceUpdatedAtUtc != (row.Date("UpdatedOn") ?? row.Date("CreatedOn"));

    // Only the target entity/key types vary; keep this batched existence read local to Report.
    private async Task<HashSet<string>> ReadTargetKeysAsync<TEntity, TKey>(
        string keyName,
        string[] values,
        CancellationToken ct
    )
        where TEntity : class
        where TKey : notnull
    {
        var parsed = values.ToDictionary(
            x => x,
            x =>
                (TKey)(
                    typeof(TKey) == typeof(Guid) ? (object)Guid.Parse(x)
                    : typeof(TKey) == typeof(int) ? int.Parse(x)
                    : x
                )
        );
        var found = new HashSet<TKey>();
        foreach (var keys in parsed.Values.Distinct().Chunk(1000))
            found.UnionWith(
                await db.Set<TEntity>()
                    .AsNoTracking()
                    .Where(x => keys.Contains(EF.Property<TKey>(x, keyName)))
                    .Select(x => EF.Property<TKey>(x, keyName))
                    .ToListAsync(ct)
            );
        return parsed.Where(x => found.Contains(x.Value)).Select(x => x.Key).ToHashSet();
    }
}
