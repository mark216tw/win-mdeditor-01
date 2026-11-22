using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Services;

namespace VibeCode.MdEditor.Cli;

internal static class CliExportCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 2;
    private const int ExitInput = 3;
    private const int ExitOutputExists = 5;

    public static async Task<int> ExecuteAsync(CliExportOptions options)
    {
        var inputPath = Path.GetFullPath(options.InputPath.Trim().Trim('"'));
        if (!File.Exists(inputPath))
        {
            throw new CliFailureException(ExitInput, $"找不到輸入檔：{inputPath}");
        }

        if (!SupportedFileTypes.IsSupported(inputPath))
        {
            throw new CliFailureException(
                ExitInput,
                $"不支援的檔案類型：{Path.GetExtension(inputPath)}。支援：{string.Join("、", SupportedFileTypes.Extensions.Order())}");
        }

        var fileInfo = new FileInfo(inputPath);
        if (fileInfo.Length > FileService.MaxSupportedFileBytes)
        {
            throw new CliFailureException(
                ExitInput,
                $"檔案大小超過上限（{FileService.MaxSupportedFileBytes / (1024 * 1024)} MB）。");
        }

        ValidateTheme(options);

        var outputPath = ResolveOutputPath(options, inputPath);

        if (File.Exists(outputPath) && !options.Force)
        {
            throw new CliFailureException(ExitOutputExists, $"輸出檔已存在：{outputPath}（請加上 --force 覆寫）");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new CliFailureException(ExitUsage, "無法判斷輸出資料夾。");
        }

        Directory.CreateDirectory(outputDirectory);

        if (options.Format == CliExportFormat.Docx)
        {
            await ExportDocxAsync(inputPath, outputPath, options.Force);
        }
        else
        {
            await GuiExportInvoker.ExportAsync(options, inputPath, outputPath);
        }

        if (options.Quiet)
        {
            return ExitSuccess;
        }

        Console.Out.WriteLine($"已匯出 {GetFormatLabel(options.Format)}：{outputPath}");
        return ExitSuccess;
    }

    private static async Task ExportDocxAsync(string inputPath, string outputPath, bool force)
    {
        var tempPath = CreateTempPath(outputPath);

        try
        {
            var fileService = new FileService();
            var document = await fileService.LoadAsync(inputPath);
            var exporter = new DocxExportService();
            await exporter.ExportAsync(
                document.Text,
                tempPath,
                Path.GetDirectoryName(inputPath),
                CancellationToken.None);

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                throw new CliFailureException(1, "Word 匯出未產生有效檔案。");
            }

            CommitOutput(tempPath, outputPath, force);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void CommitOutput(string tempPath, string outputPath, bool force)
    {
        try
        {
            if (force)
            {
                File.Move(tempPath, outputPath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, outputPath);
            }
        }
        catch (IOException) when (!force && File.Exists(outputPath))
        {
            throw new CliFailureException(ExitOutputExists, $"輸出檔已存在：{outputPath}（請加上 --force 覆寫）");
        }
    }

    private static string ResolveOutputPath(CliExportOptions options, string inputPath)
    {
        var extension = GetExtension(options.Format);
        var inputDirectory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var defaultFileName = Path.GetFileNameWithoutExtension(inputPath) + extension;

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            return Path.Combine(inputDirectory, defaultFileName);
        }

        var requested = options.OutputPath.Trim().Trim('"');
        var fullPath = Path.GetFullPath(requested);

        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, defaultFileName);
        }

        if (requested.EndsWith(Path.DirectorySeparatorChar) ||
            requested.EndsWith(Path.AltDirectorySeparatorChar))
        {
            throw new CliFailureException(ExitUsage, $"輸出資料夾不存在：{fullPath}");
        }

        var requestedExtension = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(requestedExtension))
        {
            return fullPath + extension;
        }

        if (!requestedExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new CliFailureException(
                ExitUsage,
                $"輸出檔副檔名必須是 {extension}，目前為 {requestedExtension}。");
        }

        return fullPath;
    }

    private static void ValidateTheme(CliExportOptions options)
    {
        if (options.Theme is null)
        {
            return;
        }

        if (!HtmlExportService.IsSupportedTheme(options.Theme))
        {
            throw new CliFailureException(
                ExitUsage,
                $"不支援的主題：{options.Theme}。可用主題：{string.Join("、", HtmlExportService.SupportedThemes)}");
        }
    }

    private static string CreateTempPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        return Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
        }
    }

    private static string GetExtension(CliExportFormat format)
    {
        return format switch
        {
            CliExportFormat.Docx => ".docx",
            CliExportFormat.Html => ".html",
            CliExportFormat.Pdf => ".pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    private static string GetFormatLabel(CliExportFormat format)
    {
        return format switch
        {
            CliExportFormat.Docx => "Word",
            CliExportFormat.Html => "HTML",
            CliExportFormat.Pdf => "PDF",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}
