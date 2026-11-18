using Unified.DataMigration.Services;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class SsTrainingCourseCodeTests
{
    [Theory]
    [InlineData("CourseCodeShortened", "MigratedWithDifference")]
    [InlineData("CourseCodeSuffixExhausted", "Rejected")]
    public void Findings_ExposeApprovedShorteningDiagnosticsRatherThanOpaqueHashes(string reason, string outcome)
    {
        var finding = MigrationDiagnostics.Finding(
            new DataMigrationOutcome
            {
                EntityType = "Training",
                LegacyId = "1",
                Outcome = outcome,
                Reason = reason,
            }
        );

        Assert.Equal(reason, finding.Code);
        Assert.Equal(outcome, finding.Outcome);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(60)]
    public void LengthBoundary_PreservesValidCodesAndShortensOnlyOverlengthCodes(int length)
    {
        var code = new string('x', length);

        var actual = SsTrainingWriter.AllocateCourseCode(code, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(length <= 50 ? code : new string('x', 45) + "_1", actual);
    }

    [Fact]
    public void PrefixBoundary_DoesNotSplitSurrogatePair()
    {
        var code = new string('x', 44) + "\U0001f600" + new string('y', 10);

        var actual = SsTrainingWriter.AllocateCourseCode(code, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(new string('x', 44) + "_1", actual);
    }

    [Fact]
    public void Collisions_SkipReservedAndPreviouslyAllocatedCodesWithoutChangingTheSet()
    {
        var prefix = new string('x', 45);
        var reserved = Enumerable.Range(1, 10).Select(x => prefix + "_" + x).ToHashSet(StringComparer.Ordinal);
        var first = SsTrainingWriter.AllocateCourseCode(prefix + " first", reserved);
        Assert.Equal(prefix + "_11", first);
        Assert.Equal(10, reserved.Count);
        reserved.Add(first);

        var second = SsTrainingWriter.AllocateCourseCode(prefix + " second", reserved);

        Assert.Equal(prefix + "_12", second);
    }

    [Fact]
    public void ExhaustedSuffix_RejectsRatherThanExceedingLimitOrShorteningPrefixAgain()
    {
        var prefix = new string('x', 45);
        var reserved = Enumerable.Range(1, 9999).Select(x => prefix + "_" + x).ToHashSet(StringComparer.Ordinal);

        var error = Assert.Throws<SsReject>(() => SsTrainingWriter.AllocateCourseCode(prefix + " longer", reserved));

        Assert.Equal("CourseCodeSuffixExhausted", error.Message);
    }
}
