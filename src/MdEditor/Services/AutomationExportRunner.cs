using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Services;
using VibeCode.MdEditor.Views;
using Windows.Graphics;

namespace VibeCode.MdEditor.Services;

public sealed class AutomationExportRunner(
    IFileService fileService,
    IMarkdownRenderer markdownRenderer,
    IHtmlExportService htmlExportService,
    ILogger<AutomationExportRunner> logger)
{
    private const string DocumentId = "automation-export";

    public async Task RunAsync(
        AutomationExportRequest request,
        string webTheme,
        double fontSize)
    {
        var inputPath = Path.GetFullPath(request.InputPath);
        var outputPath = Path.GetFullPath(request.OutputPath);

        ValidateInput(inputPath);

        var expectedExtension = request.Format == AutomationExportFormat.Html ? ".html" : ".pdf";
        if (!Path.GetExtension(outputPath).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationExportException(
                2,
                $"輸出檔副檔名必須是 {expectedExtension}：{outputPath}");
        }

        if (File.Exists(outputPath) && !request.Force)
        {
            throw new AutomationExportException(
                5,
                $"輸出檔已存在：{outputPath}（請加上 --force 覆寫）");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new AutomationExportException(1, "無法判斷輸出資料夾。");
        }

        Directory.CreateDirectory(outputDirectory);

        var documentDirectory = Path.GetDirectoryName(inputPath);
        var tempPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var document = await fileService.LoadAsync(inputPath);
            var html = markdownRenderer.Render(document.Text, documentDirectory);
            var title = Path.GetFileNameWithoutExtension(inputPath);

            logger.LogInformation(
                "開始自動化匯出：{Format}，輸入 {Input}，輸出 {Output}",
                request.Format,
                inputPath,
                outputPath);

            await ExportWithPreviewAsync(
                request.Format,
                html,
                title,
                documentDirectory,
                webTheme,
                fontSize,
                tempPath);

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                throw new AutomationExportException(1, "匯出未產生有效檔案。");
            }

            CommitOutput(tempPath, outputPath, request.Force);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void ValidateInput(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new AutomationExportException(3, $"找不到輸入檔：{inputPath}");
        }

        if (!SupportedFileTypes.IsSupported(inputPath))
        {
            throw new AutomationExportException(
                3,
                $"不支援的檔案類型：{Path.GetExtension(inputPath)}");
        }

        if (new FileInfo(inputPath).Length > FileService.MaxSupportedFileBytes)
        {
            throw new AutomationExportException(
                3,
                $"檔案大小超過上限（{FileService.MaxSupportedFileBytes / (1024 * 1024)} MB）。");
        }
    }

    private async Task ExportWithPreviewAsync(
        AutomationExportFormat format,
        string html,
        string title,
        string? documentDirectory,
        string webTheme,
        double fontSize,
        string outputPath)
    {
        var preview = new PreviewHostView
        {
            WebViewUserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MdEditor",
                "WebView2Automation"),
        };

        var window = new Window
        {
            Title = "MdEditor Export",
            Content = preview,
        };

        try
        {
            window.AppWindow.IsShownInSwitchers = false;
            window.AppWindow.Move(new PointInt32(-32000, -32000));
            window.AppWindow.Resize(new SizeInt32(1280, 2000));
            window.Activate();

            await preview.SetDocumentFolderAsync(documentDirectory);
            await preview.SetFontSizeAsync(fontSize);

            try
            {
                await preview.InitializeAsync();
            }
            catch (Exception ex)
            {
                throw new AutomationExportException(
                    4,
                    "無法初始化 WebView2。請確認已安裝 Microsoft Edge WebView2 Runtime。",
                    ex);
            }

            await preview.RenderAsync(
                html,
                webTheme,
                Guid.NewGuid().ToString("N"),
                DocumentId);

            if (format == AutomationExportFormat.Html)
            {
                var rendered = await preview.GetRenderedHtmlAsync();
                await htmlExportService.ExportAsync(
                    rendered,
                    title,
                    webTheme,
                    documentDirectory,
                    outputPath);
            }
            else
            {
                await preview.PrintToPdfAsync(outputPath);
            }
        }
        finally
        {
            preview.Close();
            window.Close();
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
            throw new AutomationExportException(
                5,
                $"輸出檔已存在：{outputPath}（請加上 --force 覆寫）");
        }
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
}
