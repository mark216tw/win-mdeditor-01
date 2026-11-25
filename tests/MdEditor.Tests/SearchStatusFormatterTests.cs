using VibeCode.MdEditor.Core.Helpers;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class SearchStatusFormatterTests
{
    [Fact]
    public void FormatMatchStatus_ReturnsEmpty_WhenQueryIsEmpty()
    {
        Assert.Equal(string.Empty, SearchStatusFormatter.FormatMatchStatus(string.Empty, 0, 0, true));
        Assert.Equal(string.Empty, SearchStatusFormatter.FormatMatchStatus(null, 1, 5, true));
    }

    [Fact]
    public void FormatMatchStatus_ReturnsInvalidRegexMessage_WhenRegexIsInvalid()
    {
        var text = SearchStatusFormatter.FormatMatchStatus("[unclosed", 0, 0, isValidRegex: false);
        Assert.Equal("規則運算式無效", text);
    }

    [Fact]
    public void FormatMatchStatus_ReturnsNoMatches_WhenTotalCountIsZero()
    {
        var text = SearchStatusFormatter.FormatMatchStatus("abc", 0, 0, isValidRegex: true);
        Assert.Equal("無相符項目", text);
    }

    [Fact]
    public void FormatMatchStatus_ReturnsTotalCount_WhenCurrentIndexIsZero()
    {
        var text = SearchStatusFormatter.FormatMatchStatus("abc", 0, 12, isValidRegex: true);
        Assert.Equal("12 筆相符", text);
    }

    [Fact]
    public void FormatMatchStatus_ReturnsCurrentOverTotal_WhenBothPositive()
    {
        var text = SearchStatusFormatter.FormatMatchStatus("abc", 3, 12, isValidRegex: true);
        Assert.Equal("3 / 12", text);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("hello", "hello")]
    [InlineData("line1\nline2", "")]
    [InlineData("line1\r\nline2", "")]
    public void NormalizeInitialQuery_HandlesEmptyAndMultiline(string? input, string expected)
    {
        Assert.Equal(expected, SearchStatusFormatter.NormalizeInitialQuery(input));
    }

    [Fact]
    public void NormalizeInitialQuery_RejectsOverlongSelection()
    {
        var longText = new string('a', 201);
        Assert.Equal(string.Empty, SearchStatusFormatter.NormalizeInitialQuery(longText));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("foo|bar", true)]
    [InlineData("[unclosed", false)]
    public void IsValidRegex_ValidatesPatterns(string pattern, bool expected)
    {
        Assert.Equal(expected, SearchStatusFormatter.IsValidRegex(pattern));
    }
}
