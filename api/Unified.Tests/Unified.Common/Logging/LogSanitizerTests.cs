using Unified.Common.Logging;

namespace Unified.Tests.Unified.Common.Logging;

public class LogSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UserText_WhenInputIsNullOrWhiteSpace_ReturnsNull(string? value)
    {
        var result = LogSanitizer.UserText(value);

        Assert.Null(result);
    }

    [Fact]
    public void UserText_WhenInputHasOuterWhiteSpace_ReturnsTrimmedValue()
    {
        var result = LogSanitizer.UserText("  search text  ");

        Assert.Equal("search text", result);
    }

    [Fact]
    public void UserText_WhenInputHasControlCharacters_ReplacesThemWithSpaces()
    {
        var result = LogSanitizer.UserText("line1\r\nline2\tend");

        Assert.Equal("line1  line2 end", result);
    }

    [Fact]
    public void UserText_WhenInputExceedsMaxLength_TruncatesAndAddsMarker()
    {
        var result = LogSanitizer.UserText("abcdef", maxLength: 3);

        Assert.Equal("abc...", result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(" value ", true)]
    public void HasValue_WhenInputIsChecked_ReturnsWhetherTrimmedValueExists(string? value, bool expected)
    {
        var result = LogSanitizer.HasValue(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" value ", 5)]
    public void Length_WhenInputIsChecked_ReturnsTrimmedLength(string? value, int? expected)
    {
        var result = LogSanitizer.Length(value);

        Assert.Equal(expected, result);
    }
}
