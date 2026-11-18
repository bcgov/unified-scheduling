using Unified.DataMigration.Services;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class SsSchedulingRecordIndexTests
{
    private static DataMigrationRecord Record(long id, string type, string key, string source = "SS") =>
        new()
        {
            Id = id,
            Source = source,
            EntityType = type,
            LegacyId = key,
            TargetType = "AssignmentEntry",
            TargetKey = id.ToString(),
            PayloadHash = "not-used-for-selection",
        };

    [Fact]
    public void FullMembershipMatchesPriorPredicateAcrossDisconnectedComponents()
    {
        var records = new List<DataMigrationRecord>();
        long next = 0;
        for (var i = 1; i <= 500; i++)
        {
            foreach (var type in new[] { "Assignment", "Shift", "Duty", "DutySlot" })
                records.Add(Record(++next, type, i.ToString()));
            records.Add(Record(++next, "AssignmentSeries", $"g10:assignment:{i}"));
            records.Add(Record(++next, "DutyEntry", $"duty:{i}:segment:0"));
            records.Add(Record(++next, "DutyEntry", $"duty:{i}:segment:1"));
        }
        records.Add(Record(++next, "DutyEntry", "duty:5:segment:invalid", "CASS"));
        records.Add(Record(++next, "DutyEntry", "duty:5:wrong-prefix"));
        records.Add(Record(++next, "AssignmentSeries", "g10:assignment:0500"));
        var index = new SsSchedulingRecordIndex(records);
        for (var i = 1; i <= 500; i++)
        {
            (string Type, string Id)[] nodes =
            [
                ("Assignment", i.ToString()),
                ("Duty", i.ToString()),
                ("DutySlot", i.ToString()),
                ("Shift", i.ToString()),
            ];
            var keys = nodes.Select(x => $"{x.Type}:{x.Id}").ToHashSet();
            var assignments = nodes.Where(x => x.Type == "Assignment").Select(x => x.Id).ToArray();
            var duties = nodes.Where(x => x.Type == "Duty").Select(x => x.Id).ToArray();
            var prior = records
                .Where(x => x.Source == "SS")
                .Where(x =>
                    keys.Contains($"{x.EntityType}:{x.LegacyId}")
                    || x.EntityType == "AssignmentSeries" && assignments.Any(id => x.LegacyId == $"g10:assignment:{id}")
                    || x.EntityType == "DutyEntry"
                        && duties.Any(id => x.LegacyId.StartsWith($"duty:{id}:segment:", StringComparison.Ordinal))
                )
                .Select(x => x.Id)
                .Order()
                .ToArray();
            Assert.Equal(prior, index.SelectIds(nodes));
        }
    }

    [Fact]
    public void SelectionIsDistinctStableAndDoesNotMatchIdentifierPrefixes()
    {
        var index = new SsSchedulingRecordIndex([
            Record(9, "Duty", "1"),
            Record(3, "DutyEntry", "duty:1:segment:a"),
            Record(4, "DutyEntry", "duty:10:segment:a"),
            Record(5, "DutyEntry", "duty:01:segment:a"),
        ]);
        Assert.Equal([3L, 9L], index.SelectIds([("Duty", "1"), ("Duty", "1")]));
        Assert.Empty(index.SelectIds([("Duty", "100")]));
    }
}
