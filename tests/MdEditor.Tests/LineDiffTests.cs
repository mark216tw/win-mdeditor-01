using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class LineDiffTests
{
    private readonly LineDiffService _service = new();

    [Fact]
    public void Compare_EmptyTexts_ReturnsNoLines()
    {
        var result = _service.Compare(string.Empty, string.Empty);

        Assert.Empty(result.Lines);
        Assert.False(result.IsApproximate);
    }

    [Fact]
    public void Compare_IdenticalTexts_ReturnsEqualLines()
    {
        var result = _service.Compare("alpha\nbeta", "alpha\nbeta");

        Assert.False(result.IsApproximate);
        Assert.Equal(2, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Equal(DiffLineKind.Equal, line.Kind));
        Assert.Equal("alpha", result.Lines[0].LeftText);
        Assert.Equal("alpha", result.Lines[0].RightText);
    }

    [Fact]
    public void Compare_RemovedLine_MarksLeftSide()
    {
        var result = _service.Compare("alpha\nbeta", "alpha");

        Assert.Equal(DiffLineKind.Equal, result.Lines[0].Kind);
        var removed = Assert.Single(result.Lines, line => line.Kind == DiffLineKind.Removed);
        Assert.Equal("beta", removed.LeftText);
        Assert.Equal(2, removed.LeftNumber);
        Assert.Null(removed.RightText);
    }

    [Fact]
    public void Compare_AddedLine_MarksRightSide()
    {
        var result = _service.Compare("alpha", "alpha\nbeta");

        Assert.Equal(DiffLineKind.Equal, result.Lines[0].Kind);
        var added = Assert.Single(result.Lines, line => line.Kind == DiffLineKind.Added);
        Assert.Equal("beta", added.RightText);
        Assert.Equal(2, added.RightNumber);
        Assert.Null(added.LeftText);
    }

    [Fact]
    public void Compare_ChangedLine_ProducesRemovedAndAddedRows()
    {
        var result = _service.Compare("alpha\nbeta\ngamma", "alpha\nbravo\ngamma");

        Assert.Equal(DiffLineKind.Equal, result.Lines[0].Kind);
        Assert.Contains(result.Lines, line => line.Kind == DiffLineKind.Removed && line.LeftText == "beta");
        Assert.Contains(result.Lines, line => line.Kind == DiffLineKind.Added && line.RightText == "bravo");
        Assert.Equal(DiffLineKind.Equal, result.Lines[^1].Kind);
        Assert.Equal("gamma", result.Lines[^1].LeftText);
    }

    [Fact]
    public void Compare_NormalizesLineEndings()
    {
        var result = _service.Compare("alpha\r\nbeta\r\ngamma", "alpha\nbeta\ngamma");

        Assert.All(result.Lines, line => Assert.Equal(DiffLineKind.Equal, line.Kind));
        Assert.Equal(3, result.Lines.Count);
    }

    [Fact]
    public void Compare_LargeInputFallsBackToApproximate()
    {
        var left = string.Join('\n', Enumerable.Range(0, LineDiffService.MaxLineCount + 1));
        var right = left;

        var result = _service.Compare(left, right);

        Assert.True(result.IsApproximate);
        Assert.NotEmpty(result.Lines);
    }
}
