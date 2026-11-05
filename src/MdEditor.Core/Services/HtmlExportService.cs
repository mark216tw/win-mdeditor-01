using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using VibeCode.MdEditor.Core.Helpers;

namespace VibeCode.MdEditor.Core.Services;

public interface IHtmlExportService
{
    Task ExportAsync(
        string contentHtml,
        string title,
        string theme,
        string? documentDirectory,
        string outputPath,
        CancellationToken cancellationToken = default);
}

public sealed partial class HtmlExportService : IHtmlExportService
{
    private const string DefaultKatexVersion = "0.19.1";
    private const string KatexCdnBase = "https://cdn.jsdelivr.net/npm/katex@";
    private const string DocumentHostPrefix = "https://doc.local/";
    private const long MaxEmbeddedImageBytes = 10 * 1024 * 1024;
    private const long MaxTotalEmbeddedImageBytes = 40 * 1024 * 1024;

    private static readonly HashSet<string> KnownThemes = new(StringComparer.Ordinal)
    {
        "light",
        "dark",
        "sepia",
        "morning",
        "lavender",
        "nord",
        "forest",
        "highcontrast",
    };

    private static readonly HashSet<string> DarkThemes = new(StringComparer.Ordinal)
    {
        "dark",
        "nord",
        "forest",
        "highcontrast",
    };

    private readonly string _assetDirectory;

    public HtmlExportService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Preview"))
    {
    }

    public HtmlExportService(string assetDirectory)
    {
        _assetDirectory = assetDirectory;
    }

    public async Task ExportAsync(
        string contentHtml,
        string title,
        string theme,
        string? documentDirectory,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("無法判斷 HTML 匯出資料夾。");
        }

        Directory.CreateDirectory(directory);

        var normalizedTheme = NormalizeTheme(theme);
        var previewCss = await ReadAssetTextAsync("preview.css", cancellationToken).ConfigureAwait(false);
        var highlightCss = await ReadAssetTextAsync(
                IsDarkTheme(normalizedTheme) ? "github-dark.min.css" : "github.min.css",
                cancellationToken)
            .ConfigureAwait(false);
        var katexCss = await ReadAssetTextAsync("katex.min.css", cancellationToken).ConfigureAwait(false);
        var katexVersion = await ReadKatexVersionAsync(cancellationToken).ConfigureAwait(false);
        katexCss = RewriteKatexFontUrls(katexCss, katexVersion);

        var processedHtml = await RewriteImagesAsync(contentHtml ?? string.Empty, documentDirectory, cancellationToken)
            .ConfigureAwait(false);
        var html = BuildDocument(processedHtml, title, normalizedTheme, previewCss, highlightCss, katexCss);

        await File.WriteAllTextAsync(
                fullPath,
                html,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> ReadAssetTextAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_assetDirectory, fileName);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ReadKatexVersionAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_assetDirectory, "katex-version.txt");
        if (File.Exists(path))
        {
            var version = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
            if (IsSafeVersion(version))
            {
                return version;
            }
        }

        return DefaultKatexVersion;
    }

    private static string BuildDocument(
        string contentHtml,
        string title,
        string theme,
        string previewCss,
        string highlightCss,
        string katexCss)
    {
        var safeTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Markdown" : title);
        var builder = new StringBuilder(contentHtml.Length + previewCss.Length + highlightCss.Length + katexCss.Length + 1024);

        builder.AppendLine("<!doctype html>");
        builder.AppendLine($"<html lang=\"zh-Hant\" data-theme=\"{theme}\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine(
            "  <meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; font-src https://cdn.jsdelivr.net; img-src data: http: https:; base-uri 'none'; form-action 'none';\">");
        builder.AppendLine($"  <title>{safeTitle}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine(previewCss);
        builder.AppendLine(highlightCss);
        builder.AppendLine(katexCss);
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <main class=\"markdown-body\">");
        builder.AppendLine(contentHtml);
        builder.AppendLine("  </main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }

    private async Task<string> RewriteImagesAsync(
        string html,
        string? documentDirectory,
        CancellationToken cancellationToken)
    {
        var matches = ImageTagRegex().Matches(html);
        if (matches.Count == 0)
        {
            return html;
        }

        var builder = new StringBuilder(html.Length);
        var position = 0;
        var totalEmbeddedBytes = 0L;

        foreach (Match tag in matches)
        {
            builder.Append(html, position, tag.Index - position);

            var tagText = tag.Value;
            var sourceMatch = ImageSourceRegex().Match(tagText);
            if (sourceMatch.Success)
            {
                var result = await CreateImageSourceAsync(
                        sourceMatch.Groups["url"].Value,
                        documentDirectory,
                        totalEmbeddedBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

                totalEmbeddedBytes += result.EmbeddedBytes;

                if (result.Source is null)
                {
                    tagText = tagText.Remove(sourceMatch.Index, sourceMatch.Length);
                }
                else
                {
                    tagText = tagText[..sourceMatch.Index] +
                        sourceMatch.Groups["prefix"].Value +
                        result.Source +
                        "\"" +
                        tagText[(sourceMatch.Index + sourceMatch.Length)..];
                }
            }

            builder.Append(tagText);
            position = tag.Index + tag.Length;
        }

        builder.Append(html, position, html.Length - position);
        return builder.ToString();
    }

    private async Task<ImageSourceResult> CreateImageSourceAsync(
        string url,
        string? documentDirectory,
        long totalEmbeddedBytes,
        CancellationToken cancellationToken)
    {
        var trimmed = url.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "#")
        {
            return new ImageSourceResult(null, 0);
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return new ImageSourceResult("https:" + trimmed, 0);
        }

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
             !trimmed.StartsWith(DocumentHostPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return new ImageSourceResult(trimmed, 0);
        }

        if (!trimmed.StartsWith(DocumentHostPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(documentDirectory))
        {
            return new ImageSourceResult(null, 0);
        }

        var relativePath = trimmed[DocumentHostPrefix.Length..];
        var fragmentIndex = relativePath.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            relativePath = relativePath[..fragmentIndex];
        }

        var queryIndex = relativePath.IndexOf('?');
        if (queryIndex >= 0)
        {
            relativePath = relativePath[..queryIndex];
        }

        var path = PathSafety.TryResolveRelativePathWithinDirectory(documentDirectory, relativePath);
        if (path is null)
        {
            return new ImageSourceResult(null, 0);
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists ||
            fileInfo.Length <= 0 ||
            fileInfo.Length > MaxEmbeddedImageBytes ||
            totalEmbeddedBytes + fileInfo.Length > MaxTotalEmbeddedImageBytes)
        {
            return new ImageSourceResult(null, 0);
        }

        var mimeType = GetImageMimeType(path);
        if (mimeType is null)
        {
            return new ImageSourceResult(null, 0);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var source = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        return new ImageSourceResult(source, bytes.LongLength);
    }

    private static string? GetImageMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".avif" => "image/avif",
            _ => null,
        };
    }

    private static string RewriteKatexFontUrls(string css, string version)
    {
        if (string.IsNullOrEmpty(css))
        {
            return css;
        }

        return KatexFontUrlRegex().Replace(css, match =>
        {
            var fileName = match.Groups["file"].Value;
            return $"url({KatexCdnBase}{version}/dist/fonts/{fileName})";
        });
    }

    private static string NormalizeTheme(string? theme)
    {
        var normalized = theme?.Trim().ToLowerInvariant();
        return normalized is not null && KnownThemes.Contains(normalized) ? normalized : "light";
    }

    private static bool IsDarkTheme(string theme)
    {
        return DarkThemes.Contains(theme);
    }

    private static bool IsSafeVersion(string version)
    {
        return !string.IsNullOrEmpty(version) && SafeVersionRegex().IsMatch(version);
    }

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(@"(?<prefix>\bsrc\s*=\s*"")(?<url>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"url\(\s*(?<quote>[""']?)fonts/(?<file>[^)""']+\.(?:woff2|woff|ttf))(?:\k<quote>)\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KatexFontUrlRegex();

    [GeneratedRegex(@"^[0-9A-Za-z.\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeVersionRegex();

    private readonly record struct ImageSourceResult(string? Source, long EmbeddedBytes);
}
