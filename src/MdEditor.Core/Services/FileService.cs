using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public interface IFileService
{
    Task<LoadedDocument> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task<FileStamp> SaveAsync(
        string path,
        string text,
        string encodingName,
        int codePage,
        bool hasBom,
        string lineEnding,
        CancellationToken cancellationToken = default);

    Task<FileStamp> ComputeStampAsync(string path, CancellationToken cancellationToken = default);

    Task<string> SavePastedImageAsync(
        string documentDirectory,
        byte[] bytes,
        string? mimeType,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class FileService : IFileService
{
    static FileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<LoadedDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);

        return await Task.Run(
            () =>
            {
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                var lastWrite = File.GetLastWriteTimeUtc(fullPath);

                var decoded = Decode(bytes);
                var lineEnding = DetectLineEnding(decoded.Text);
                var normalized = decoded.Text.Replace("\r\n", "\n").Replace('\r', '\n');

                return new LoadedDocument(
                    normalized,
                    decoded.EncodingName,
                    decoded.CodePage,
                    decoded.HasBom,
                    lineEnding,
                    hash,
                    new DateTimeOffset(lastWrite, TimeSpan.Zero),
                    bytes.Length);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileStamp> SaveAsync(
        string path,
        string text,
        string encodingName,
        int codePage,
        bool hasBom,
        string lineEnding,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("無法判斷檔案目錄。");
        }

        Directory.CreateDirectory(directory);

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var finalText = lineEnding.Equals("CRLF", StringComparison.OrdinalIgnoreCase)
            ? normalized.Replace("\n", "\r\n")
            : normalized;

        var encoding = ResolveEncoding(encodingName, codePage, hasBom);
        var preamble = hasBom ? encoding.GetPreamble() : [];
        var content = encoding.GetBytes(finalText);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                if (preamble.Length > 0)
                {
                    await stream.WriteAsync(preamble, cancellationToken).ConfigureAwait(false);
                }

                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (preamble.Length > 0)
        {
            incrementalHash.AppendData(preamble);
        }

        incrementalHash.AppendData(content);
        var hash = Convert.ToHexString(incrementalHash.GetHashAndReset());
        var lastWrite = File.GetLastWriteTimeUtc(fullPath);
        return new FileStamp(
            hash,
            new DateTimeOffset(lastWrite, TimeSpan.Zero),
            preamble.Length + content.Length);
    }

    public async Task<FileStamp> ComputeStampAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);

        return await Task.Run(
            () =>
            {
                var lastWrite = File.GetLastWriteTimeUtc(fullPath);
                return new FileStamp(
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    new DateTimeOffset(lastWrite, TimeSpan.Zero),
                    bytes.Length);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(Path.GetFullPath(path)));
    }

    public async Task<string> SavePastedImageAsync(
        string documentDirectory,
        byte[] bytes,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentDirectory))
        {
            throw new ArgumentException("必須提供文件所在資料夾。", nameof(documentDirectory));
        }

        if (bytes is null || bytes.Length == 0)
        {
            throw new ArgumentException("圖片內容不可為空。", nameof(bytes));
        }

        var fullDirectory = Path.GetFullPath(documentDirectory);
        var imagesDirectory = Path.Combine(fullDirectory, "images");
        Directory.CreateDirectory(imagesDirectory);

        var extension = ResolveImageExtension(mimeType);
        var baseName = $"paste-{DateTime.Now:yyyyMMdd-HHmmss}";

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var fileName = attempt == 0
                ? $"{baseName}{extension}"
                : $"{baseName}-{attempt}{extension}";
            var fullPath = Path.Combine(imagesDirectory, fileName);

            try
            {
                await using var stream = new FileStream(
                    fullPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                return $"images/{fileName}";
            }
            catch (IOException) when (File.Exists(fullPath))
            {
            }
        }

        throw new IOException("無法建立唯一的貼上圖片檔案名稱。");
    }

    private static string ResolveImageExtension(string? mimeType)
    {
        var normalized = mimeType?.Split(';')[0].Trim().ToLowerInvariant();
        return normalized switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ => ".png",
        };
    }

    private static DecodedText Decode(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return new DecodedText(Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4), "UTF-32 LE", 12000, true);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return new DecodedText(new UTF32Encoding(true, true).GetString(bytes, 4, bytes.Length - 4), "UTF-32 BE", 12001, true);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new DecodedText(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8", 65001, true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new DecodedText(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16 LE", 1200, true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new DecodedText(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16 BE", 1201, true);
        }

        var strictUtf8 = new UTF8Encoding(false, true);
        try
        {
            return new DecodedText(strictUtf8.GetString(bytes), "UTF-8", 65001, false);
        }
        catch (DecoderFallbackException)
        {
            var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            if (codePage <= 0)
            {
                codePage = 1252;
            }

            var encoding = Encoding.GetEncoding(
                codePage,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback);
            return new DecodedText(encoding.GetString(bytes), encoding.WebName.ToUpperInvariant(), codePage, false);
        }
    }

    private static string DetectLineEnding(string text)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\r')
                {
                    crlf++;
                }
                else
                {
                    lf++;
                }
            }
            else if (text[i] == '\r')
            {
                cr++;
            }
        }

        return (crlf > 0 || cr > 0) && crlf >= lf ? "CRLF" : "LF";
    }

    private static Encoding ResolveEncoding(string encodingName, int codePage, bool hasBom)
    {
        return encodingName.ToUpperInvariant() switch
        {
            "UTF-8" => new UTF8Encoding(hasBom),
            "UTF-16 LE" => new UnicodeEncoding(false, hasBom),
            "UTF-16 BE" => new UnicodeEncoding(true, hasBom),
            "UTF-32 LE" => new UTF32Encoding(false, hasBom),
            "UTF-32 BE" => new UTF32Encoding(true, hasBom),
            _ => Encoding.GetEncoding(
                codePage <= 0 ? 65001 : codePage,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback),
        };
    }

    private sealed record DecodedText(string Text, string EncodingName, int CodePage, bool HasBom);
}
