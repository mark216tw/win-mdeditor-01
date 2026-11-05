using System.Text.RegularExpressions;
using Markdig;
using VibeCode.MdEditor.Core.Helpers;

namespace VibeCode.MdEditor.Core.Services;

public interface IMarkdownRenderer
{
    string Render(string markdown, string? documentDirectory);
}

public sealed partial class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer()
        : this(MarkdownPipelineFactory.Create(includeLineNumbers: true))
    {
    }

    public MarkdownRenderer(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public string Render(string markdown, string? documentDirectory)
    {
        var html = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        html = RewriteImages(html, documentDirectory);
        return SanitizeUrls(html);
    }

    private static string RewriteImages(string html, string? documentDirectory)
    {
        if (string.IsNullOrWhiteSpace(documentDirectory))
        {
            return html;
        }

        var fullDocumentDirectory = Path.GetFullPath(documentDirectory);

        return ImageTagRegex().Replace(html, imageTag =>
            ImageSourceRegex().Replace(imageTag.Value, source =>
            {
                var url = source.Groups["url"].Value;
                if (IsAbsoluteUrl(url))
                {
                    return source.Value;
                }

                var mapped = TryMapRelativeUrl(url, fullDocumentDirectory);
                return mapped is null
                    ? source.Groups["prefix"].Value + "#\""
                    : source.Groups["prefix"].Value + mapped + "\"";
            }));
    }

    private static string SanitizeUrls(string html)
    {
        return UrlAttributeRegex().Replace(html, match =>
        {
            var url = match.Groups["url"].Value.TrimStart();
            var isSource = match.Groups["name"].Value.Equals("src", StringComparison.OrdinalIgnoreCase);

            var blocked =
                url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
                (!isSource && url.StartsWith("data:", StringComparison.OrdinalIgnoreCase));

            return blocked
                ? match.Groups["prefix"].Value + "#\""
                : match.Value;
        });
    }

    private static bool IsAbsoluteUrl(string url)
    {
        var trimmed = url.TrimStart();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("#", StringComparison.Ordinal) ||
               trimmed.StartsWith("/", StringComparison.Ordinal) ||
               trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryMapRelativeUrl(string url, string fullDocumentDirectory)
    {
        var value = url;
        var query = string.Empty;
        var fragment = string.Empty;

        var fragmentIndex = value.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            fragment = value[fragmentIndex..];
            value = value[..fragmentIndex];
        }

        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = value[queryIndex..];
            value = value[..queryIndex];
        }

        var resolvedPath = PathSafety.TryResolveRelativePathWithinDirectory(fullDocumentDirectory, value);
        if (resolvedPath is null)
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(fullDocumentDirectory, resolvedPath);
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            return null;
        }

        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = Uri.EscapeDataString(segments[i]);
        }

        return "https://doc.local/" + string.Join('/', segments) + query + fragment;
    }

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(@"(?<prefix>\bsrc\s*=\s*"")(?<url>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"(?<prefix>\b(?<name>href|src)\s*=\s*"")(?<url>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlAttributeRegex();
}
