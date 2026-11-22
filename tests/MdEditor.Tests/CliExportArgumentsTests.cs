using VibeCode.MdEditor.Core.Helpers;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class CliExportArgumentsTests
{
    [Fact]
    public void TryParse_DocxWithDefaults()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "docx", "note.md"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(CliExportFormat.Docx, options.Format);
        Assert.Equal("note.md", options.InputPath);
        Assert.Null(options.OutputPath);
        Assert.Null(options.Theme);
        Assert.False(options.Force);
        Assert.False(options.Quiet);
    }

    [Fact]
    public void TryParse_HtmlWithAllOptions()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "html", "note.md", "-o", "out.html", "--theme", "dark", "--force", "--quiet"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(CliExportFormat.Html, options.Format);
        Assert.Equal("out.html", options.OutputPath);
        Assert.Equal("dark", options.Theme);
        Assert.True(options.Force);
        Assert.True(options.Quiet);
    }

    [Fact]
    public void TryParse_SupportsEqualsSyntax()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "pdf", "--output=out.pdf", "note.md", "--theme=sepia-light"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(CliExportFormat.Pdf, options.Format);
        Assert.Equal("out.pdf", options.OutputPath);
        Assert.Equal("sepia-light", options.Theme);
    }

    [Fact]
    public void TryParse_SupportsEndOfOptionsMarker()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "docx", "--", "-note.md"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal("-note.md", options.InputPath);
    }

    [Fact]
    public void TryParse_RejectsMissingInput()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "docx"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("缺少輸入檔案", error);
    }

    [Fact]
    public void TryParse_RejectsMissingOptionValue()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "docx", "note.md", "--output"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("--output 缺少值", error);
    }

    [Fact]
    public void TryParse_RejectsUnknownOption()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "pdf", "note.md", "--unknown"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("未知選項", error);
    }

    [Fact]
    public void TryParse_RejectsThemeForDocx()
    {
        var parsed = CliExportArguments.TryParse(
            ["export", "docx", "note.md", "--theme", "dark"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("docx", error);
    }

    [Fact]
    public void TryParse_RejectsUnknownCommand()
    {
        var parsed = CliExportArguments.TryParse(
            ["open", "note.md"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Contains("未知命令", error);
    }
}
