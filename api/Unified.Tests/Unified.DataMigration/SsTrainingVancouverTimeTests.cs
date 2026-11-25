using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;

namespace Unified.Tests.DataMigration;

// America/Vancouver moved to a permanent UTC-07:00 offset on 2026-11-01. SS stored standard-time training
// instants at UTC-08:00, so trainings of sheriffs whose home location is America/Vancouver move -1h from then on.
public sealed class SsTrainingVancouverTimeTests
{
    private static readonly TimeSpan Pst = TimeSpan.FromHours(-8);
    private static readonly TimeSpan Mst = TimeSpan.FromHours(-7);

    private static DateTimeOffset Z(string value) => DateTimeOffset.Parse(value).ToUniversalTime();

    [Theory]
    // Old-rule standard time on/after the change: SS local+8h -> local+7h.
    [InlineData("2026-12-01T17:00:00Z", "2026-12-01T16:00:00Z")]
    [InlineData("2027-01-15T16:00:00Z", "2027-01-15T15:00:00Z")]
    [InlineData("2027-11-07T09:00:00Z", "2027-11-07T08:00:00Z")] // 2027 old-rule fall-back instant.
    // 2026-11-01 fall-back: 01:30 PDT is before the change; the repeated 01:30 PST is shifted.
    [InlineData("2026-11-01T08:30:00Z", "2026-11-01T08:30:00Z")]
    [InlineData("2026-11-01T09:00:00Z", "2026-11-01T08:00:00Z")]
    [InlineData("2026-11-01T09:30:00Z", "2026-11-01T08:30:00Z")]
    // 2027 spring-forward: 01:59 PST shifted, 03:00 PDT (old-rule DST) unchanged.
    [InlineData("2027-03-14T09:59:00Z", "2027-03-14T08:59:00Z")]
    [InlineData("2027-03-14T10:00:00Z", "2027-03-14T10:00:00Z")]
    // Old-rule DST after the change: SS already stored local+7h.
    [InlineData("2027-07-15T16:00:00Z", "2027-07-15T16:00:00Z")]
    // Before the change Unified renders the historical -08:00 offset, so standard time is already correct.
    [InlineData("2026-01-20T17:00:00Z", "2026-01-20T17:00:00Z")]
    [InlineData("2009-12-01T17:00:00Z", "2009-12-01T17:00:00Z")]
    public void VancouverInstants_ShiftOnlyOldRuleStandardTimeFromTheChange(string source, string expected)
    {
        Assert.Equal(Z(expected), SsTrainingRules.AdjustVancouverStandardTime(Z(source), vancouver: true));
        Assert.Equal(Z(source), SsTrainingRules.AdjustVancouverStandardTime(Z(source), vancouver: false));
    }

    [Fact]
    public void ShiftedInstant_ShowsTheSsWallClockAtTheNewOffset()
    {
        // SS: 2026-12-01 09:00 PST entered in America/Vancouver.
        var ss = new DateTimeOffset(2026, 12, 1, 9, 0, 0, Pst);
        var mapped = SsTrainingRules.AdjustVancouverStandardTime(ss.ToUniversalTime(), vancouver: true);
        Assert.Equal(Z("2026-12-01T16:00:00Z"), mapped);
        Assert.Equal(ss.DateTime, mapped.ToOffset(Mst).DateTime);
    }

    [Fact]
    public void VancouverSheriffs_UseHomeLocationTimezone_ExactTrimmedOrdinal()
    {
        string[] zones =
        [
            "America/Vancouver",
            " America/Vancouver ",
            "america/vancouver",
            "America/Edmonton",
            "America/Creston",
            "America/Dawson_Creek",
            "America/Fort_Nelson",
            "Etc/UTC",
            "",
        ];
        var locations = zones
            .Select((zone, i) => Row(("Id", (i + 1).ToString()), ("Timezone", zone)))
            .Append(Row(("Id", "99"), ("Timezone", null)))
            .ToList();
        var users = zones
            .Select((_, i) => Row(("Id", $"u{i + 1}"), ("HomeLocationId", i + 1)))
            .Append(Row(("Id", "u99"), ("HomeLocationId", 99)))
            .Append(Row(("Id", "no-home"), ("HomeLocationId", null)))
            .Append(Row(("Id", "missing-location"), ("HomeLocationId", 500)))
            .ToList();

        var vancouver = SsTrainingRules.VancouverSheriffs(users, locations);

        Assert.Equal(["u1", "u2"], vancouver.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void MapHistoryTimes_ShiftsEachEndpointByItsOwnOldRuleOffset_AndExpiry()
    {
        // Spring-forward span: 01:00 PST -> 04:00 PDT; only the standard-time start moves.
        var spring = SsTrainingRules.MapHistoryTimes(
            History("2027-03-14T09:00:00Z", "2027-03-14T11:00:00Z", "2027-12-31T07:59:59Z"),
            vancouver: true
        );
        Assert.Equal(
            (Z("2027-03-14T08:00:00Z"), Z("2027-03-14T11:00:00Z"), (DateTimeOffset?)Z("2027-12-31T06:59:59Z"), true),
            (spring.Start, spring.End, spring.Expiry, spring.Adjusted)
        );

        // Year-end certification expiry (SS endOf('year') at -08:00) keeps its calendar date.
        var expiry = SsTrainingRules.MapHistoryTimes(
            History("2027-07-15T16:00:00Z", "2027-07-15T23:00:00Z", "2028-01-01T07:59:59Z"),
            vancouver: true
        );
        Assert.Equal(new DateTime(2027, 12, 31, 23, 59, 59), expiry.Expiry!.Value.ToOffset(Mst).DateTime);
        Assert.Equal((Z("2027-07-15T16:00:00Z"), Z("2027-07-15T23:00:00Z")), (expiry.Start, expiry.End));

        // Fall-back span shorter than the shift: start (PDT) unchanged, end (PST) clamped to the start.
        var fallBack = SsTrainingRules.MapHistoryTimes(
            History("2026-11-01T08:30:00Z", "2026-11-01T09:15:00Z", null),
            vancouver: true
        );
        Assert.Equal((Z("2026-11-01T08:30:00Z"), Z("2026-11-01T08:30:00Z")), (fallBack.Start, fallBack.End));
        Assert.True(fallBack.End >= fallBack.Start);

        // Not a Vancouver sheriff, or nothing in the affected window: unchanged and not adjusted.
        foreach (
            var (row, vancouver) in new[]
            {
                (History("2026-12-01T17:00:00Z", "2026-12-02T01:00:00Z", "2027-12-01T17:00:00Z"), false),
                (History("2026-01-20T17:00:00Z", "2026-01-21T01:00:00Z", "2026-06-30T06:59:59Z"), true),
            }
        )
        {
            var times = SsTrainingRules.MapHistoryTimes(row, vancouver);
            Assert.False(times.Adjusted);
            Assert.Equal((row.Date("StartDate"), row.Date("EndDate")), (times.Start, times.End));
        }
        // Only the expiry in the window: still adjusted.
        Assert.True(
            SsTrainingRules
                .MapHistoryTimes(History("2026-01-20T17:00:00Z", "2026-01-21T01:00:00Z", "2027-01-01T07:59:59Z"), true)
                .Adjusted
        );
        // Source validation is unchanged.
        Assert.Equal(
            "ReversedDates",
            Assert
                .Throws<SsReject>(() =>
                    SsTrainingRules.MapHistoryTimes(History("2026-12-02T17:00:00Z", "2026-12-01T17:00:00Z", null), true)
                )
                .Message
        );
    }

    [Fact]
    public void MappedTarget_IsAlwaysTimed()
    {
        // SS has no all-day concept; migrated rows are AllDay=false, so the shift applies to every migrated row.
        var row = History("2026-12-01T17:00:00Z", "2026-12-02T01:00:00Z", null);
        var times = SsTrainingRules.MapHistoryTimes(row, true);
        var target = SsTrainingRules.MappedTarget(row, Guid.Empty, 1, "First Aid", times);
        Assert.False(target.AllDay);
        Assert.Equal((Z("2026-12-01T16:00:00Z"), Z("2026-12-02T00:00:00Z")), (target.AwardedOn, target.EndingOn));
        Assert.NotEqual(target, target with { AllDay = true });
    }

    [Fact]
    public void HistoryHash_UnadjustedKeepsLegacyShape_AdjustedAddsMappedInstants()
    {
        // Nothing in the affected window (expiry is a pre-change date): legacy hash shape.
        var row = History("2026-01-20T17:00:00Z", "2026-01-21T01:00:00Z", "2026-06-20T07:00:00Z");
        var legacy = SsWriteSession.Hash(
            new
            {
                User = "u1",
                Course = row.Int("TrainingTypeId"),
                dates = (Start: row.Date("StartDate")!.Value, End: row.Date("EndDate")!.Value),
                Expiry = row.Date("TrainingCertificationExpiry"),
                Notice = row.Bool("FirstNotice"),
            }
        );
        Assert.Equal(legacy, SsTrainingRules.HistoryHash(row, "u1", SsTrainingRules.MapHistoryTimes(row, true)));

        var later = History("2026-12-01T17:00:00Z", "2026-12-02T01:00:00Z", null);
        var unadjusted = SsTrainingRules.HistoryHash(later, "u1", SsTrainingRules.MapHistoryTimes(later, false));
        var adjusted = SsTrainingRules.HistoryHash(later, "u1", SsTrainingRules.MapHistoryTimes(later, true));
        Assert.NotEqual(unadjusted, adjusted);
        Assert.Equal(adjusted, SsTrainingRules.HistoryHash(later, "u1", SsTrainingRules.MapHistoryTimes(later, true)));
    }

    [Fact]
    public void HistoryOrder_UsesMappedValues()
    {
        var sheriff = Guid.NewGuid();
        // Raw order A (08:50Z PDT) then B (09:10Z PST); mapped B ends 08:10Z, before A.
        var a = History("2026-11-01T08:00:00Z", "2026-11-01T08:50:00Z", null, "1", sheriff);
        var b = History("2026-11-01T08:05:00Z", "2026-11-01T09:10:00Z", null, "2", sheriff);
        Assert.Equal(["1", "2"], SsTrainingRules.HistoryOrder([b, a], new HashSet<string>()).Select(x => x.Id));
        Assert.Equal(
            ["2", "1"],
            SsTrainingRules.HistoryOrder([a, b], new HashSet<string> { sheriff.ToString() }).Select(x => x.Id)
        );
    }

    private static SsRow Row(params (string Key, object? Value)[] values) =>
        new(values.ToDictionary(x => x.Key, x => x.Value));

    private static SsRow History(string start, string end, string? expiry, string id = "1", Guid? sheriff = null) =>
        Row(
            ("Id", id),
            ("SheriffId", sheriff ?? Guid.Empty),
            ("TrainingTypeId", 7),
            ("StartDate", Z(start).UtcDateTime),
            ("EndDate", Z(end).UtcDateTime),
            ("TrainingCertificationExpiry", expiry is null ? null : Z(expiry).UtcDateTime),
            ("FirstNotice", false),
            ("Comment", null),
            ("Note", null)
        );
}
