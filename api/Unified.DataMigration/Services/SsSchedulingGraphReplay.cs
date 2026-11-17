using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

/// <summary>
/// Replaces a changed connected source graph, never a partial merge member.
/// Old and proposed memberships form the atomic boundary. Every physical root
/// must have an original ownership/hash receipt; unknown dependents fail closed.
/// </summary>
internal sealed class SsSchedulingGraphReplay(UnifiedDbContext db)
{
    private static readonly string[] RootTypes =
    [
        "Assignment",
        "Shift",
        "Duty",
        "DutySlot",
        "DutyEntry",
        "AssignmentSeries",
    ];

    private sealed record Node(string Type, SsRow Row)
    {
        public string Key => $"{Type}:{Row.Id}";
    }

    private sealed record Component(List<Node> Nodes, string Hash);

    private static SsRow Synthetic(string id) =>
        new(
            new Dictionary<string, object?>
            {
                ["Id"] = id,
                ["CreatedOn"] = null,
                ["UpdatedOn"] = null,
            }
        );

    private static List<Component> Plan(
        Dictionary<string, List<SsRow>> rows,
        DateTimeOffset boundary,
        HashSet<string> retained
    )
    {
        var duties = rows["Duty"]
            .Where(x => x.Date("EndDate") >= boundary || retained.Contains($"Duty:{x.Id}"))
            .ToList();
        var dutyIds = duties.Select(x => x.Int("Id")).ToHashSet();
        var referencedAssignments = duties.Select(x => x.Int("AssignmentId")).ToHashSet();
        var dutyById = duties.ToDictionary(x => x.Int("Id")!.Value);
        var nodes = rows["Assignment"]
            .Where(x =>
                x.Date("ExpiryDate") is null
                || referencedAssignments.Contains(x.Int("Id"))
                || retained.Contains($"Assignment:{x.Id}")
            )
            .Select(x => new Node("Assignment", x))
            .Concat(
                rows["Shift"]
                    .Where(x => x.Date("EndDate") >= boundary || retained.Contains($"Shift:{x.Id}"))
                    .Select(x => new Node("Shift", x))
            )
            .Concat(duties.Select(x => new Node("Duty", x)))
            .Concat(
                rows["DutySlot"]
                    .Where(x =>
                        (x.Date("EndDate") >= boundary || retained.Contains($"DutySlot:{x.Id}"))
                        && dutyIds.Contains(x.Int("DutyId"))
                    )
                    .Select(x => new Node("DutySlot", x))
            )
            .ToList();
        var parents = nodes.ToDictionary(x => x.Key, x => x.Key);
        string Find(string key)
        {
            while (parents[key] != key)
            {
                parents[key] = parents[parents[key]];
                key = parents[key];
            }
            return key;
        }
        void Join(string a, string b)
        {
            if (parents.ContainsKey(a) && parents.ContainsKey(b))
                parents[Find(a)] = Find(b);
        }
        var lookup = rows["LookupCode"].ToDictionary(x => x.Id);
        foreach (
            var group in nodes
                .Where(x => x.Type == "Assignment")
                .GroupBy(x =>
                {
                    lookup.TryGetValue(x.Row.Int("LookupCodeId")?.ToString() ?? "", out var code);
                    var name = string.IsNullOrWhiteSpace(x.Row.Text("Name"))
                        ? code?.Text("Code")
                        : $"{code?.Text("Code")} ({x.Row.Text("Name")})";
                    return (x.Row.Int("LocationId"), Type: code?.Int("Type"), Name: name?.Trim().ToUpperInvariant());
                })
        )
        foreach (var node in group)
            Join(group.First().Key, node.Key);
        DateOnly Day(SsRow row)
        {
            var instant = row.Date("StartDate") ?? DateTimeOffset.MinValue;
            try
            {
                return DateOnly.FromDateTime(
                    TimeZoneInfo
                        .ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(row.Text("Timezone") ?? ""))
                        .DateTime
                );
            }
            catch (TimeZoneNotFoundException)
            {
                return DateOnly.FromDateTime(instant.UtcDateTime);
            }
        }
        foreach (
            var group in nodes
                .Where(x => x.Type == "Shift")
                .GroupBy(x =>
                    (
                        x.Row.Guid("SheriffId"),
                        x.Row.Int("LocationId"),
                        Day(x.Row),
                        Cancelled: x.Row.Date("ExpiryDate") is not null
                    )
                )
        )
        {
            Node? prior = null;
            DateTimeOffset? end = null;
            foreach (var node in group.OrderBy(x => x.Row.Date("StartDate")).ThenBy(x => x.Row.Int("Id")))
            {
                if (prior is not null && node.Row.Date("StartDate") <= end)
                    Join(prior.Key, node.Key);
                else
                    prior = node;
                end = end is null || node.Row.Date("EndDate") > end ? node.Row.Date("EndDate") : end;
            }
        }
        foreach (var duty in nodes.Where(x => x.Type == "Duty"))
            Join(duty.Key, $"Assignment:{duty.Row.Int("AssignmentId")}");
        var shiftsBySheriffLocation = nodes
            .Where(x => x.Type == "Shift")
            .ToLookup(x => (x.Row.Guid("SheriffId"), x.Row.Int("LocationId")));
        foreach (var slot in nodes.Where(x => x.Type == "DutySlot"))
        {
            Join(slot.Key, $"Duty:{slot.Row.Int("DutyId")}");
            if (slot.Row.Date("ExpiryDate") is not null)
                continue;
            var duty = dutyById[slot.Row.Int("DutyId")!.Value];
            foreach (
                var shift in shiftsBySheriffLocation[(slot.Row.Guid("SheriffId"), duty.Int("LocationId"))]
                    .Where(x =>
                        slot.Row.Guid("SheriffId") is not null
                        && x.Row.Date("StartDate") < slot.Row.Date("EndDate")
                        && x.Row.Date("EndDate") > slot.Row.Date("StartDate")
                    )
            )
                Join(slot.Key, shift.Key);
        }
        object Mapped(Node n)
        {
            var r = n.Row.ProjectNullable(
                "LocationId",
                "SheriffId",
                "AssignmentId",
                "DutyId",
                "StartDate",
                "EndDate",
                "Timezone",
                "ExpiryDate",
                "Comment",
                "Name",
                "LookupCodeId",
                "Start",
                "End",
                "AdhocStartDate",
                "AdhocEndDate",
                "Sunday",
                "Monday",
                "Tuesday",
                "Wednesday",
                "Thursday",
                "Friday",
                "Saturday",
                "IsClosed",
                "IsNotRequired",
                "IsNotAvailable",
                "AnticipatedAssignmentId"
            );
            lookup.TryGetValue(r.Int("LookupCodeId")?.ToString() ?? "", out var code);
            return new
            {
                n.Key,
                Location = r.Int("LocationId"),
                User = r.Guid("SheriffId"),
                Assignment = r.Int("AssignmentId"),
                Duty = r.Int("DutyId"),
                Start = r.Date("StartDate"),
                End = r.Date("EndDate"),
                Zone = r.Text("Timezone"),
                Expiry = r.Date("ExpiryDate"),
                Comment = r.Text("Comment"),
                Name = r.Text("Name"),
                Code = code?.Text("Code"),
                Type = code?.Int("Type"),
                DefaultStart = r.Time("Start"),
                DefaultEnd = r.Time("End"),
                AdhocStart = r.Date("AdhocStartDate"),
                AdhocEnd = r.Date("AdhocEndDate"),
                Weekdays = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
                    .Select(r.Bool)
                    .ToArray(),
                Closed = r.Bool("IsClosed"),
                NotRequired = r.Bool("IsNotRequired"),
                NotAvailable = r.Bool("IsNotAvailable"),
                AnticipatedAssignment = r.Int("AnticipatedAssignmentId"),
            };
        }
        return nodes
            .GroupBy(x => Find(x.Key))
            .Select(g =>
            {
                var members = g.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
                return new Component(members, SsWriteSession.Hash(members.Select(Mapped).ToArray()));
            })
            .ToList();
    }

    private static Dictionary<string, List<SsRow>> Subset(Dictionary<string, List<SsRow>> rows, IEnumerable<Node> nodes)
    {
        var keys = nodes.Select(x => x.Key).ToHashSet();
        return rows.ToDictionary(
            x => x.Key,
            x => x.Key == "LookupCode" ? x.Value : x.Value.Where(r => keys.Contains($"{x.Key}:{r.Id}")).ToList()
        );
    }

    internal async Task<Dictionary<string, List<SsRow>>> ReplayAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        DateTimeOffset boundary,
        Func<Dictionary<string, List<SsRow>>, Task> write,
        CancellationToken ct,
        List<SsSchedulingProposal> proposals
    )
    {
        var members = await db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == "SchedulingMember")
            .ToListAsync(ct);
        var proposed = Plan(rows, boundary, members.Select(x => x.LegacyId).ToHashSet());
        var old = members.GroupBy(x => (x.TargetType, x.TargetKey)).ToList();
        var excluded = new HashSet<string>();
        var seen = new HashSet<int>();
        for (var index = 0; index < proposed.Count; index++)
        {
            if (!seen.Add(index))
                continue;
            var family = new HashSet<int> { index };
            var keys = proposed[index].Nodes.Select(x => x.Key).ToHashSet();
            var oldMembers = new List<DataMigrationRecord>();
            bool expanded;
            do
            {
                expanded = false;
                foreach (var group in old.Where(g => g.Any(m => keys.Contains(m.LegacyId))))
                foreach (var member in group)
                {
                    if (!oldMembers.Contains(member))
                        oldMembers.Add(member);
                    if (keys.Add(member.LegacyId))
                        expanded = true;
                }
                for (var i = 0; i < proposed.Count; i++)
                    if (proposed[i].Nodes.Any(n => keys.Contains(n.Key)) && family.Add(i))
                    {
                        foreach (var n in proposed[i].Nodes)
                            keys.Add(n.Key);
                        expanded = true;
                    }
            } while (expanded);
            seen.UnionWith(family);
            if (oldMembers.Count == 0)
                continue; // First apply: writers establish original proofs.
            if (
                family.Count == 1
                && oldMembers.Count == proposed[index].Nodes.Count
                && oldMembers.All(x => x.PayloadHash == proposed[index].Hash)
            )
                continue;
            var nodes = family.SelectMany(i => proposed[i].Nodes).ToList();
            excluded.UnionWith(keys);
            var first = nodes.First();
            await s.AtomicGraphAsync(
                first.Type,
                first.Row,
                async () =>
                {
                    if (oldMembers.Any(m => !nodes.Any(n => n.Key == m.LegacyId)))
                        throw new SsReject("GraphMissingFullSourceMembership");
                    if (s.Run.RollbackJournalKeyId is null)
                        throw new SsReject("GraphReplacementRequiresInverseJournal");
                    var records = await SelectRecordsAsync(nodes, ct);
                    await s.GuardGraphAsync(records, ct);
                    var keep = await RetainedTargetsAsync(records, nodes, proposals, ct);
                    await DetachChangedSeriesAsync(records, keep, s, ct);
                    await DeleteGraphAsync(records, keep, ct);
                    foreach (var record in records.Where(x => !keep.Contains((x.TargetType, x.TargetKey))))
                        s.Rebinding.Add((record.EntityType, record.LegacyId));
                    await write(Subset(rows, nodes));
                    var failure = s.SourceOutcomes.FirstOrDefault(x =>
                        nodes.Any(n => n.Type == x.EntityType && n.Row.Id == x.LegacyId)
                        && x.Outcome is "Rejected" or "Failed" or "NeedsAction"
                    );
                    if (failure is not null)
                        throw new SsReject(failure.Reason ?? "GraphProposalRequiresAction");
                    foreach (
                        var record in records.Where(x => s.Rebinding.Contains((x.EntityType, x.LegacyId))).ToList()
                    )
                    {
                        if (record.EntityType == "DutySlot")
                        {
                            var slot = rows["DutySlot"].Single(x => x.Id == record.LegacyId);
                            if (slot.Date("ExpiryDate") is null)
                                throw new SsReject("GraphUnresolvedLiveSlot");
                            var duty = await s.ResolveAsync("Duty", slot.Int("DutyId")?.ToString(), ct);
                            await s.LinkAsync(
                                "DutySlot",
                                slot,
                                "AssignmentEntry",
                                duty,
                                SsWriteSession.Hash(new { slot.Id, Deleted = true }),
                                false,
                                ct,
                                "Excluded",
                                "DeletedSlot"
                            );
                        }
                        else if (record.EntityType is "DutyEntry" or "AssignmentSeries")
                        {
                            // Obsolete synthetic composition, not a missing source business row.
                            await RemoveSyntheticAsync([record], ct);
                            s.Rebinding.Remove((record.EntityType, record.LegacyId));
                        }
                        else
                            throw new SsReject("GraphUnresolvedCrosswalk");
                    }
                    await db.SaveChangesAsync(ct);
                    await RemoveSyntheticAsync(oldMembers, ct);
                    var roots = oldMembers.Select(x => (x.TargetType, x.TargetKey)).ToList();
                    var snapshots = await db
                        .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == "SchedulingComponent")
                        .ToListAsync(ct);
                    await RemoveSyntheticAsync(
                        snapshots.Where(x => roots.Contains((x.TargetType, x.TargetKey))).ToList(),
                        ct
                    );
                    await db.SaveChangesAsync(ct);
                    foreach (var i in family)
                        await CaptureOneAsync(proposed[i], s, ct);
                },
                ct,
                nodes.Select(x => (x.Type, x.Row.Id)).ToList()
            );
            var outcome = s.Outcomes.LastOrDefault(x => x.EntityType == first.Type && x.LegacyId == first.Row.Id);
            if (outcome?.Outcome is "Rejected" or "Failed")
                foreach (var node in nodes)
                    s.Outcome(node.Type, node.Row.Id, outcome.Outcome, outcome.Reason);
        }

        return rows.ToDictionary(
            x => x.Key,
            x => x.Key == "LookupCode" ? x.Value : x.Value.Where(r => !excluded.Contains($"{x.Key}:{r.Id}")).ToList()
        );
    }

    private async Task RemoveSyntheticAsync(List<DataMigrationRecord> records, CancellationToken ct)
    {
        if (
            records.Any(x =>
                x.EntityType is not ("SchedulingComponent" or "SchedulingMember" or "DutyEntry" or "AssignmentSeries")
            )
        )
            throw new SsReject("CannotRemovePrimarySourceCrosswalk");
        var ids = records.Select(x => x.Id).ToArray();
        // The inverse journal retains immutable encrypted before-images of this
        // obsolete synthetic composition and its receipts. Primary SS crosswalks
        // and their mutation history are rebound, never deleted.
        db.DataMigrationMutations.RemoveRange(
            await db.DataMigrationMutations.Where(x => ids.Contains(x.DataMigrationRecordId)).ToListAsync(ct)
        );
        await db.SaveChangesAsync(ct);
        db.DataMigrationRecords.RemoveRange(records);
        await db.SaveChangesAsync(ct);
    }

    internal async Task CaptureAsync(
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession s,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        var retained = await db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == "SchedulingMember")
            .Select(x => x.LegacyId)
            .ToListAsync(ct);
        foreach (var component in Plan(rows, boundary, retained.ToHashSet()))
        {
            var keys = component.Nodes.Select(x => (x.Type, x.Row.Id)).ToHashSet();
            if (
                s.SourceOutcomes.Any(o =>
                    o.Outcome is "Rejected" or "Failed" or "NeedsAction" && keys.Contains((o.EntityType, o.LegacyId))
                )
            )
                continue;
            if (
                await db.DataMigrationRecords.AnyAsync(
                    x => x.Source == "SS" && x.EntityType == "SchedulingMember" && x.LegacyId == component.Nodes[0].Key,
                    ct
                )
            )
                continue;
            // A missing/drifted initial proof is a primary failure, not an
            // ignorable synthetic metric. Persist all member failures before
            // the next batch checkpoint so Report cannot pass this lineage.
            await s.AtomicGraphAsync(
                component.Nodes[0].Type,
                component.Nodes[0].Row,
                () => CaptureOneAsync(component, s, ct),
                ct,
                component.Nodes.Select(x => (x.Type, x.Row.Id)).ToList()
            );
        }
    }

    private async Task<List<DataMigrationRecord>> SelectRecordsAsync(List<Node> nodes, CancellationToken ct)
    {
        var assignments = nodes.Where(x => x.Type == "Assignment").Select(x => x.Row.Id).ToHashSet();
        var duties = nodes.Where(x => x.Type == "Duty").Select(x => x.Row.Id).ToHashSet();
        var keys = nodes.Select(x => x.Key).ToHashSet();
        return (
            await db
                .DataMigrationRecords.Where(x => x.Source == "SS" && RootTypes.Contains(x.EntityType))
                .ToListAsync(ct)
        )
            .Where(x =>
                keys.Contains($"{x.EntityType}:{x.LegacyId}")
                || x.EntityType == "AssignmentSeries" && assignments.Any(id => x.LegacyId == $"g10:assignment:{id}")
                || x.EntityType == "DutyEntry"
                    && duties.Any(id => x.LegacyId.StartsWith($"duty:{id}:segment:", StringComparison.Ordinal))
            )
            .ToList();
    }

    private async Task CaptureOneAsync(Component component, SsWriteSession s, CancellationToken ct)
    {
        var records = await SelectRecordsAsync(component.Nodes, ct);
        if (records.Count == 0)
            return;
        await s.GuardGraphAsync(records, ct);
        await CheckDependentsAsync(records, ct);
        var root = records.First(x => x.CreatedByRunId is not null);
        // Guards hold database row locks, not EF tracking leases. All these
        // records are read-only here; detaching does not release a lock/savepoint.
        db.ChangeTracker.Clear();
        await s.LinkAsync(
            "SchedulingComponent",
            Synthetic(component.Nodes[0].Key),
            root.TargetType,
            root.TargetKey,
            component.Hash,
            false,
            ct
        );
        var captured = 0;
        foreach (var node in component.Nodes)
        {
            await s.LinkAsync(
                "SchedulingMember",
                Synthetic(node.Key),
                root.TargetType,
                root.TargetKey,
                component.Hash,
                false,
                ct
            );
            if (++captured % 100 == 0)
            {
                // LinkAsync leaves its final mutation pending. Flush it BEFORE
                // detaching; the entire component still shares the outer savepoint.
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task CheckDependentsAsync(List<DataMigrationRecord> records, CancellationToken ct)
    {
        int[] Ids(string type) =>
            records.Where(x => x.TargetType == type).Select(x => int.Parse(x.TargetKey)).Distinct().ToArray();
        var definitions = Ids("AssignmentDefinition");
        var shifts = Ids("ShiftEntry");
        var entries = Ids("AssignmentEntry");
        var series = Ids("AssignmentSeries");
        if (
            await db.AssignmentEntries.AnyAsync(
                x =>
                    (
                        definitions.Contains(x.AssignmentDefinitionId)
                        || x.AssignmentSeriesId != null && series.Contains(x.AssignmentSeriesId.Value)
                    ) && !entries.Contains(x.Id),
                ct
            )
            || await db.AssignmentSeries.AnyAsync(
                x => definitions.Contains(x.AssignmentDefinitionId) && !series.Contains(x.Id),
                ct
            )
            || await db.ShiftAssignmentEntries.AnyAsync(
                x =>
                    (shifts.Contains(x.ShiftEntryId) && !entries.Contains(x.AssignmentEntryId))
                    || (entries.Contains(x.AssignmentEntryId) && !shifts.Contains(x.ShiftEntryId)),
                ct
            )
            || await db.ShiftAssignmentSeriesLinks.AnyAsync(x => series.Contains(x.AssignmentSeriesId), ct)
        )
            throw new SsReject("UnownedGraphDependent");
    }

    private async Task<HashSet<(string Type, string Key)>> RetainedTargetsAsync(
        List<DataMigrationRecord> records,
        List<Node> nodes,
        List<SsSchedulingProposal> proposals,
        CancellationToken ct
    )
    {
        var keep = new HashSet<(string Type, string Key)>();
        bool Same(SsSchedulingProposal p, out DataMigrationRecord? owner)
        {
            var matches = records.Where(x => x.EntityType == p.Type && p.Members.Contains(x.LegacyId)).ToList();
            owner = matches.FirstOrDefault();
            return owner is not null
                && matches.Count == p.Members.Length
                && matches.All(x => x.PayloadHash == p.Hash && x.TargetKey == matches[0].TargetKey)
                && !records.Any(x =>
                    x.EntityType == p.Type && x.TargetKey == matches[0].TargetKey && !p.Members.Contains(x.LegacyId)
                );
        }
        foreach (var p in proposals.Where(x => x.Type is "Assignment" or "Shift"))
            if (Same(p, out var owner))
                keep.Add((owner!.TargetType, owner.TargetKey));
        foreach (var p in proposals.Where(x => x.Type == "Duty"))
        {
            if (!Same(p, out _))
                continue;
            var duty = p.Members.Single();
            var slotIds = nodes
                .Where(x => x.Type == "DutySlot" && x.Row.Int("DutyId")?.ToString() == duty)
                .Select(x => x.Row.Id)
                .ToHashSet();
            var ids = records
                .Where(x =>
                    x.EntityType == "Duty" && x.LegacyId == duty
                    || x.EntityType == "DutyEntry"
                        && x.LegacyId.StartsWith($"duty:{duty}:segment:", StringComparison.Ordinal)
                    || x.EntityType == "DutySlot" && slotIds.Contains(x.LegacyId)
                )
                .Select(x => int.Parse(x.TargetKey))
                .Distinct()
                .ToArray();
            var entries = await db.AssignmentEntries.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            var links = await db.ShiftAssignmentEntries.Where(x => ids.Contains(x.AssignmentEntryId)).ToListAsync(ct);
            if (
                entries.All(x => keep.Contains(("AssignmentDefinition", x.AssignmentDefinitionId.ToString())))
                && links.All(x => keep.Contains(("ShiftEntry", x.ShiftEntryId.ToString())))
            )
                foreach (var id in ids)
                    keep.Add(("AssignmentEntry", id.ToString()));
        }
        foreach (var p in proposals.Where(x => x.Type == "AssignmentSeries"))
        {
            if (!Same(p, out var owner))
                continue;
            var id = int.Parse(owner!.TargetKey);
            var series = await db.AssignmentSeries.SingleAsync(x => x.Id == id, ct);
            var entries = await db
                .AssignmentEntries.Where(x => x.AssignmentSeriesId == id)
                .Select(x => x.Id)
                .ToListAsync(ct);
            if (
                keep.Contains(("AssignmentDefinition", series.AssignmentDefinitionId.ToString()))
                && entries.All(x => keep.Contains(("AssignmentEntry", x.ToString())))
            )
                keep.Add(("AssignmentSeries", id.ToString()));
        }
        return keep;
    }

    private async Task DetachChangedSeriesAsync(
        List<DataMigrationRecord> records,
        HashSet<(string Type, string Key)> keep,
        SsWriteSession s,
        CancellationToken ct
    )
    {
        var removed = records
            .Where(x => x.TargetType == "AssignmentSeries" && !keep.Contains((x.TargetType, x.TargetKey)))
            .Select(x => int.Parse(x.TargetKey))
            .ToArray();
        var ids = keep.Where(x => x.Type == "AssignmentEntry").Select(x => int.Parse(x.Key)).ToArray();
        var entries = await db
            .AssignmentEntries.Include(x => x.Event)
            .Where(x =>
                ids.Contains(x.Id) && x.AssignmentSeriesId != null && removed.Contains(x.AssignmentSeriesId.Value)
            )
            .ToListAsync(ct);
        if (entries.Count == 0)
            return;
        var duties = records
            .Where(x => x.EntityType == "Duty" && entries.Any(e => e.Id.ToString() == x.TargetKey))
            .Select(x => x.LegacyId)
            .ToArray();
        await s.ComposeAsync(
            "Duty",
            duties,
            async () =>
            {
                foreach (var entry in entries)
                {
                    entry.AssignmentSeriesId = null;
                    entry.AssignmentSeries = null;
                    entry.Event!.EventSeriesId = null;
                    entry.Event.EventSeries = null;
                    entry.Event.SeriesStartAtUtc = null;
                    entry.Event.SeriesEndAtUtc = null;
                }
                await db.SaveChangesAsync(ct);
            },
            ct
        );
    }

    private async Task DeleteGraphAsync(
        List<DataMigrationRecord> records,
        HashSet<(string Type, string Key)> keep,
        CancellationToken ct
    )
    {
        await CheckDependentsAsync(records, ct);
        int[] Ids(string type) =>
            records
                .Where(x => x.TargetType == type && !keep.Contains((x.TargetType, x.TargetKey)))
                .Select(x => int.Parse(x.TargetKey))
                .Distinct()
                .ToArray();
        var assignmentIds = Ids("AssignmentEntry");
        var shiftIds = Ids("ShiftEntry");
        var seriesIds = Ids("AssignmentSeries");
        var definitionIds = Ids("AssignmentDefinition");
        var entries = await db.AssignmentEntries.Where(x => assignmentIds.Contains(x.Id)).ToListAsync(ct);
        var shifts = await db.ShiftEntries.Where(x => shiftIds.Contains(x.Id)).ToListAsync(ct);
        var series = await db.AssignmentSeries.Where(x => seriesIds.Contains(x.Id)).ToListAsync(ct);
        var entryIds = entries.Select(x => x.Id).ToArray();
        var links = await db
            .ShiftAssignmentEntries.Include(x => x.Users)
            .Where(x => entryIds.Contains(x.AssignmentEntryId))
            .ToListAsync(ct);
        db.ShiftAssignmentEntries.RemoveRange(links);
        await db.SaveChangesAsync(ct);
        db.AssignmentEntries.RemoveRange(entries);
        await db.SaveChangesAsync(ct);
        db.Events.RemoveRange(
            await db.Events.Where(x => entries.Select(e => e.EventId).Contains(x.Id)).ToListAsync(ct)
        );
        db.AssignmentSeries.RemoveRange(series);
        db.ShiftEntries.RemoveRange(shifts);
        await db.SaveChangesAsync(ct);
        db.Events.RemoveRange(await db.Events.Where(x => shifts.Select(e => e.EventId).Contains(x.Id)).ToListAsync(ct));
        db.EventSeries.RemoveRange(
            await db.EventSeries.Where(x => series.Select(e => e.EventSeriesId).Contains(x.Id)).ToListAsync(ct)
        );
        db.AssignmentDefinitions.RemoveRange(
            await db.AssignmentDefinitions.Where(x => definitionIds.Contains(x.Id)).ToListAsync(ct)
        );
        await db.SaveChangesAsync(ct);
    }
}
