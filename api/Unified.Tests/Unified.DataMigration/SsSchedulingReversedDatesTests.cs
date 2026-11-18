using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;

namespace Unified.Tests.DataMigration;

public sealed class SsSchedulingReversedDatesTests
{
    private static readonly DateTime Early = new(2022, 4, 13, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Late = new(2022, 4, 14, 3, 0, 0, DateTimeKind.Utc);

    private static SsRow Row(int id, DateTime? start, DateTime? end) =>
        new(
            new Dictionary<string, object?>
            {
                ["Id"] = id,
                ["StartDate"] = start,
                ["EndDate"] = end,
            }
        );

    [Theory]
    [InlineData("Shift")]
    [InlineData("Duty")]
    [InlineData("DutySlot")]
    public void ReversedSchedulingRowIsSwappedAndNoLongerRejected(string table)
    {
        var rows = SsSchedulingWriter.SwapReversedDates(new() { [table] = [Row(1, Late, Early)] });

        var dates = SsWriteSession.Dates(rows[table][0]);

        Assert.Equal(new DateTimeOffset(Early), dates.Start);
        Assert.Equal(new DateTimeOffset(Late), dates.End);
    }

    [Fact]
    public void OrderedIncompleteAndNonSchedulingRowsAreLeftUnchanged()
    {
        var ordered = Row(1, Early, Late);
        var missingEnd = Row(2, Late, null);
        var other = Row(3, Late, Early);

        var rows = SsSchedulingWriter.SwapReversedDates(
            new() { ["DutySlot"] = [ordered, missingEnd], ["Assignment"] = [other] }
        );

        Assert.Same(ordered, rows["DutySlot"][0]);
        Assert.Same(missingEnd, rows["DutySlot"][1]);
        Assert.Same(other, rows["Assignment"][0]);
        Assert.Equal("ReversedDates", Assert.Throws<SsReject>(() => SsWriteSession.Dates(other)).Message);
    }
}
