using System.Text.RegularExpressions;
using Markdig;

namespace VibeCode.MdEditor.Core.Services;

public interface IMarkdownRenderer
{
    string Render(string markdown, string? documentDirectory);
}

public sealed partial class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer()
        : this(MarkdownPipelineFactory.Create())
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

        return ImageTagRegex().Replace(html, imageTag =>
            ImageSourceRegex().Replace(imageTag.Value, source =>
            {
                var url = source.Groups["url"].Value;
                if (IsAbsoluteUrl(url))
                {
                    return source.Value;
                }

                var mapped = "https://doc.local/" + EncodeRelativeUrl(url);
                return source.Groups["prefix"].Value + mapped + "\"";
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

    private static string EncodeRelativeUrl(string url)
    {
        var value = url.Replace('\\', '/');
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

        var segments = value.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment is "." or ".." or "" || segment.Contains('%'))
            {
                continue;
            }

            segments[i] = Uri.EscapeDataString(segment);
        }

        return string.Join('/', segments) + query + fragment;
    }

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(@"(?<prefix>\bsrc\s*=\s*"")(?<url>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"(?<prefix>\b(?<name>href|src)\s*=\s*"")(?<url>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlAttributeRegex();
}
