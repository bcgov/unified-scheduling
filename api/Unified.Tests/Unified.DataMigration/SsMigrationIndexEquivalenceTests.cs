using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

// Equivalence checks for the indexed replacements of linear outcome/graph scans.
public sealed class SsMigrationIndexEquivalenceTests
{
    private sealed class Member(string legacyId)
    {
        public string LegacyId { get; } = legacyId;
    }

    // Verbatim shape of the original ReplayAsync family loop.
    private static (List<int> Family, HashSet<string> Keys, List<Member> OldMembers) OriginalExpand(
        int index,
        List<List<string>> components,
        List<List<Member>> old
    )
    {
        var family = new HashSet<int> { index };
        var keys = components[index].ToHashSet();
        var oldMembers = new List<Member>();
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
            for (var i = 0; i < components.Count; i++)
                if (components[i].Any(keys.Contains) && family.Add(i))
                {
                    foreach (var n in components[i])
                        keys.Add(n);
                    expanded = true;
                }
        } while (expanded);
        return (family.ToList(), keys, oldMembers);
    }

    [Fact]
    public void FamilyExpansion_MatchesOriginalPassLoopOrderOnRandomOverlappingGraphs()
    {
        for (var seed = 0; seed < 400; seed++)
        {
            var random = new Random(seed);
            var keySpace = random.Next(3, 40);
            string Key() => $"k{random.Next(keySpace)}";
            // Proposed components may overlap here (stricter than Plan's disjoint partition), and
            // old groups straddle them, so both forward and backward (next-pass) wake-ups occur.
            var components = Enumerable
                .Range(0, random.Next(1, 25))
                .Select(_ => Enumerable.Range(0, random.Next(1, 4)).Select(_ => Key()).Distinct().ToList())
                .ToList();
            var old = Enumerable
                .Range(0, random.Next(0, 15))
                .Select(_ => Enumerable.Range(0, random.Next(1, 4)).Select(_ => new Member(Key())).ToList())
                .ToList();
            var expansion = new FamilyExpansion<Member>(
                components.Select(x => (IReadOnlyList<string>)x).ToList(),
                old.Select(x => (IReadOnlyList<Member>)x).ToList(),
                x => x.LegacyId
            );
            for (var index = 0; index < components.Count; index++)
            {
                var expected = OriginalExpand(index, components, old);
                var actual = expansion.Expand(index);
                Assert.Equal(expected.Family, actual.Family);
                Assert.True(expected.Keys.SetEquals(actual.Keys), $"seed {seed} index {index}");
                Assert.Equal(expected.OldMembers, actual.OldMembers);
            }
        }
    }

    [Fact]
    public void FamilyExpansion_WakesLaterGroupsInTheSamePass()
    {
        // Group 0 wakes group 1 within the same pass, which then wakes component 0 ahead of 1.
        var components = new List<List<string>>
        {
            new() { "k0" },
            new() { "k2" },
            new() { "k1" },
        };
        var a = new Member("k1");
        var b = new Member("k2");
        var c = new Member("k0");
        var old = new List<List<Member>>
        {
            new() { a, b },
            new() { c, b },
        };
        var expected = OriginalExpand(2, components, old);
        var actual = new FamilyExpansion<Member>(
            components.Select(x => (IReadOnlyList<string>)x).ToList(),
            old.Select(x => (IReadOnlyList<Member>)x).ToList(),
            x => x.LegacyId
        ).Expand(2);
        Assert.Equal([2, 0, 1], actual.Family);
        Assert.Equal(expected.Family, actual.Family);
        Assert.Equal(expected.OldMembers, actual.OldMembers);
    }

    private static SsWriteSession Session() =>
        new(
            null!,
            new DataMigrationRun
            {
                Source = "SS",
                Status = "Running",
                StartedOn = DateTimeOffset.UnixEpoch,
            }
        );

    private static SsRow Row(string id) => new(new Dictionary<string, object?> { ["Id"] = id });

    [Fact]
    public void OutcomeIndex_MatchesLinearScansAndReportsEveryTouchedKey()
    {
        var random = new Random(7);
        var session = Session();
        session.TrackOutcomeChanges();
        var flushed = new Dictionary<(string, string), (string, string?)>();
        string[] types = ["Duty", "Shift", "SchedulingValidation"];
        string[] outcomes = ["Migrated", "Rejected", "Failed", "Excluded", "NeedsAction"];
        for (var step = 0; step < 2000; step++)
        {
            var type = types[random.Next(types.Length)];
            var id = random.Next(60).ToString(System.Globalization.CultureInfo.InvariantCulture);
            session.Outcome(type, id, outcomes[random.Next(outcomes.Length)], random.Next(3) == 0 ? null : "R");
            if (step % 37 != 0)
                continue;
            var touched = session.TakeChangedOutcomeKeys().ToHashSet();
            foreach (var outcome in session.Outcomes)
            {
                var key = (outcome.EntityType, outcome.LegacyId);
                // Anything not touched since the previous take still has its previous value.
                if (!touched.Contains(key))
                    Assert.Equal(flushed[key], (outcome.Outcome, outcome.Reason));
                flushed[key] = (outcome.Outcome, outcome.Reason);
            }
        }
        foreach (var type in types)
            for (var i = 0; i < 61; i++)
            {
                var id = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.Same(
                    session.Outcomes.LastOrDefault(x => x.EntityType == type && x.LegacyId == id),
                    session.FindOutcome(type, id)
                );
                Assert.Equal(
                    session.Outcomes.FindLastIndex(x => x.EntityType == type && x.LegacyId == id),
                    session.OutcomePosition(type, id)
                );
            }
    }

    [Fact]
    public void CompleteGroupOutcomes_UsesLatestRecordedMemberOutcomeAndFillsTheRest()
    {
        var session = Session();
        session.Outcome("Shift", "2", "Rejected", "First");
        session.Outcome("Duty", "9", "Migrated");
        session.Outcome("Shift", "1", "Failed", "Latest");
        session.Outcome("Shift", "2", "Rejected", "Updated in place");
        SsSchedulingWriter.CompleteGroupOutcomes(session, "Shift", [Row("2"), Row("1"), Row("3"), Row("4")]);
        Assert.Equal(
            [
                ("Shift", "2", "Rejected", "Updated in place"),
                ("Duty", "9", "Migrated", null),
                ("Shift", "1", "Failed", "Latest"),
                ("Shift", "3", "Failed", "Latest"),
                ("Shift", "4", "Failed", "Latest"),
            ],
            session.Outcomes.Select(x => (x.EntityType, x.LegacyId, x.Outcome, x.Reason)).ToArray()
        );

        var empty = Session();
        SsSchedulingWriter.CompleteGroupOutcomes(empty, "Assignment", [Row("5")]);
        Assert.Equal(("Failed", "GroupWriteFailed"), (empty.Outcomes[0].Outcome, empty.Outcomes[0].Reason));
    }
}
