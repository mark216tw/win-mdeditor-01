using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class HtmlExportServiceTests
{
    [Fact]
    public async Task Export_WrapsHtmlWithThemeAndKatexCdn()
    {
        using var temp = new TempDirectory();
        var assets = await CreateAssetsAsync(temp);
        var output = temp.Combine("out.html");

        var service = new HtmlExportService(assets);
        await service.ExportAsync(
            "<p>Hello</p>",
            "我的文件 <x>",
            "dark",
            temp.DirectoryPath,
            output);

        var html = await File.ReadAllTextAsync(output);

        Assert.Contains("data-theme=\"dark\"", html);
        Assert.Contains("<title>我的文件 &lt;x&gt;</title>", html);
        Assert.Contains("default-src 'none'", html);
        Assert.Contains(".hljs{color:#fff}", html);
        Assert.Contains(
            "https://cdn.jsdelivr.net/npm/katex@0.19.1/dist/fonts/KaTeX_Main-Regular.woff2",
            html);
        Assert.Contains("<p>Hello</p>", html);
    }

    [Fact]
    public async Task Export_UsesLightHighlightForLightTheme()
    {
        using var temp = new TempDirectory();
        var assets = await CreateAssetsAsync(temp);
        var output = temp.Combine("light.html");

        var service = new HtmlExportService(assets);
        await service.ExportAsync("<p>Hello</p>", "Light", "light", temp.DirectoryPath, output);

        var html = await File.ReadAllTextAsync(output);

        Assert.Contains("data-theme=\"light\"", html);
        Assert.Contains(".hljs{color:#000}", html);
        Assert.DoesNotContain(".hljs{color:#fff}", html);
    }

    [Fact]
    public async Task Export_EmbedsLocalImagesAndKeepsRemoteImages()
    {
        using var temp = new TempDirectory();
        var assets = await CreateAssetsAsync(temp);
        var documentDirectory = Path.Combine(temp.DirectoryPath, "docs");
        var imagesDirectory = Path.Combine(documentDirectory, "images");
        Directory.CreateDirectory(imagesDirectory);

        var onePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(Path.Combine(imagesDirectory, "inside.png"), onePixelPng);

        var content =
            "<img src=\"https://doc.local/images/inside.png\" alt=\"inside\">" +
            "<img src=\"https://example.com/remote.png\" alt=\"remote\">" +
            "<img src=\"//cdn.example.com/protocol.png\" alt=\"protocol\">" +
            "<img src=\"https://doc.local/../outside.png\" alt=\"outside\">";

        var output = temp.Combine("images.html");
        var service = new HtmlExportService(assets);
        await service.ExportAsync(content, "Images", "light", documentDirectory, output);

        var html = await File.ReadAllTextAsync(output);

        Assert.Contains("data:image/png;base64,", html);
        Assert.Contains("https://example.com/remote.png", html);
        Assert.Contains("https://cdn.example.com/protocol.png", html);
        Assert.DoesNotContain("src=\"#\"", html);
        Assert.DoesNotContain("../outside.png", html);
        Assert.DoesNotContain("src=\"https://doc.local/", html);
    }

    private static async Task<string> CreateAssetsAsync(TempDirectory temp)
    {
        var assets = Path.Combine(temp.DirectoryPath, "assets");
        Directory.CreateDirectory(assets);

        await File.WriteAllTextAsync(
            Path.Combine(assets, "preview.css"),
            ":root{--bg:#fff;--fg:#000}");
        await File.WriteAllTextAsync(
            Path.Combine(assets, "github.min.css"),
            ".hljs{color:#000}");
        await File.WriteAllTextAsync(
            Path.Combine(assets, "github-dark.min.css"),
            ".hljs{color:#fff}");
        await File.WriteAllTextAsync(
            Path.Combine(assets, "katex.min.css"),
            "@font-face{src:url(fonts/KaTeX_Main-Regular.woff2) format(\"woff2\")}");
        await File.WriteAllTextAsync(
            Path.Combine(assets, "katex-version.txt"),
            "0.19.1\n");

        return assets;
    }
}
