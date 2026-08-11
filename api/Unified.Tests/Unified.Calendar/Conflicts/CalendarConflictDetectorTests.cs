using Unified.Calendar.Conflicts;

namespace Unified.Tests.Calendar.Conflicts;

public sealed class CalendarConflictDetectorTests
{
    private static readonly Guid ResourceA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ResourceB = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Baseline = new(2026, 7, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly CalendarConflictDetector _detector = new();

    [Fact]
    public void Detect_WhenIntervalsOverlap_ReturnsActualIntersection()
    {
        var conflicts = _detector.Detect([Participant(1, ResourceA, 0, 120), Participant(2, ResourceA, 60, 180)]);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(Baseline.AddMinutes(60), conflict.OverlapStart);
        Assert.Equal(Baseline.AddMinutes(120), conflict.OverlapEnd);
    }

    [Fact]
    public void Detect_WhenIntervalsTouchAtBoundary_DoesNotConflict()
    {
        var conflicts = _detector.Detect([Participant(1, ResourceA, 0, 60), Participant(2, ResourceA, 60, 120)]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Detect_WhenResourcesDiffer_DoesNotConflict()
    {
        var conflicts = _detector.Detect([Participant(1, ResourceA, 0, 60), Participant(2, ResourceB, 30, 90)]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Detect_WhenEventHasDuplicateResourceRows_DoesNotConflictWithItself()
    {
        var conflicts = _detector.Detect([Participant(1, ResourceA, 0, 60), Participant(1, ResourceA, 0, 60)]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Detect_WhenThreeIntervalsOverlap_ReturnsEachPairOnce()
    {
        var conflicts = _detector.Detect([
            Participant(3, ResourceA, 20, 80),
            Participant(1, ResourceA, 0, 60),
            Participant(2, ResourceA, 10, 70),
        ]);

        Assert.Equal(3, conflicts.Count);
        Assert.Equal(3, conflicts.Select(conflict => conflict.Id).Distinct().Count());
    }

    [Fact]
    public void Detect_IsDeterministicRegardlessOfInputOrder()
    {
        CalendarConflictParticipant[] participants =
        [
            Participant(2, ResourceA, 30, 90),
            Participant(1, ResourceA, 0, 60),
            Participant(4, ResourceB, 15, 75),
            Participant(3, ResourceB, 0, 60),
        ];

        var forward = _detector.Detect(participants).Select(conflict => conflict.Id);
        var reverse = _detector.Detect(participants.Reverse().ToArray()).Select(conflict => conflict.Id);

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void Detect_IgnoresZeroAndNegativeLengthIntervals()
    {
        var conflicts = _detector.Detect([
            Participant(1, ResourceA, 30, 30),
            Participant(2, ResourceA, 60, 30),
            Participant(3, ResourceA, 0, 90),
        ]);

        Assert.Empty(conflicts);
    }

    private static CalendarConflictParticipant Participant(int id, Guid resourceId, int startMinutes, int endMinutes) =>
        new(
            id,
            "assignment",
            "scheduling",
            resourceId,
            Baseline.AddMinutes(startMinutes),
            Baseline.AddMinutes(endMinutes),
            $"Event {id}"
        );
}
