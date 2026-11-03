using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void Render_FormatsGfmExtensions()
    {
        var html = _renderer.Render(
            "# 標題\n\n**粗體** 與 ~~刪除~~\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n",
            null);

        Assert.Contains("<h1", html);
        Assert.Contains("<strong>粗體</strong>", html);
        Assert.Contains("<del>刪除</del>", html);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public void Render_RemovesRawHtml()
    {
        var html = _renderer.Render(
            "<script>alert('x')</script>\n\n正常文字",
            null);

        Assert.DoesNotContain("<script", html);
        Assert.Contains("正常文字", html);
    }

    [Fact]
    public void Render_RewritesRelativeImageToDocumentHost()
    {
        var html = _renderer.Render(
            "![替代文字](images/my%20image.png)",
            @"C:\temp\docs");

        Assert.Contains("https://doc.local/images/my%20image.png", html);
    }

    [Fact]
    public void Render_KeepsAbsoluteImage()
    {
        var html = _renderer.Render(
            "![遠端](https://example.com/a.png)",
            @"C:\temp\docs");

        Assert.Contains("https://example.com/a.png", html);
        Assert.DoesNotContain("https://doc.local/https", html);
    }

    [Fact]
    public void Render_BlocksJavascriptLinks()
    {
        var html = _renderer.Render("[點我](javascript:alert(1))", null);

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }
}
