using VibeCode.MdEditor.Core.Helpers;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class AutomationExportArgumentsTests
{
    [Theory]
    [InlineData("--automation-export")]
    [InlineData("--automation-export=html")]
    [InlineData("--AUTOMATION-EXPORT=pdf")]
    public void IsAutomationRequest_DetectsSwitch(string argument)
    {
        Assert.True(AutomationExportArguments.IsAutomationRequest(["MdEditor.exe", argument]));
    }

    [Fact]
    public void IsAutomationRequest_ReturnsFalseForNormalLaunch()
    {
        Assert.False(AutomationExportArguments.IsAutomationRequest(["note.md"]));
    }

    [Fact]
    public void Parse_HtmlRequest()
    {
        var request = AutomationExportArguments.Parse(
            ["--automation-export", "html", "--input", "note.md", "--output", "out.html", "--theme", "dark"]);

        Assert.Equal(AutomationExportFormat.Html, request.Format);
        Assert.Equal("note.md", request.InputPath);
        Assert.Equal("out.html", request.OutputPath);
        Assert.Equal("dark", request.Theme);
        Assert.False(request.Force);
    }

    [Fact]
    public void Parse_PdfRequestWithForce()
    {
        var request = AutomationExportArguments.Parse(
            ["--automation-export=pdf", "--input=note.md", "--output=out.pdf", "--force"]);

        Assert.Equal(AutomationExportFormat.Pdf, request.Format);
        Assert.True(request.Force);
    }

    [Fact]
    public void Parse_RejectsMissingOutput()
    {
        var exception = Assert.Throws<FormatException>(() =>
            AutomationExportArguments.Parse(
                ["--automation-export", "html", "--input", "note.md"]));

        Assert.Contains("--output", exception.Message);
    }

    [Fact]
    public void Parse_RejectsUnknownFormat()
    {
        var exception = Assert.Throws<FormatException>(() =>
            AutomationExportArguments.Parse(
                ["--automation-export", "docx", "--input", "note.md", "--output", "out.docx"]));

        Assert.Contains("不支援", exception.Message);
    }

    [Fact]
    public void Parse_RejectsUnknownArgument()
    {
        var exception = Assert.Throws<FormatException>(() =>
            AutomationExportArguments.Parse(
                ["--automation-export", "html", "--input", "note.md", "--output", "out.html", "--unknown"]));

        Assert.Contains("未知", exception.Message);
    }
}
