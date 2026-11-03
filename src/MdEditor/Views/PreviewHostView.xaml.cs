using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;

namespace VibeCode.MdEditor.Views;

public sealed partial class PreviewHostView : UserControl, IPreviewHost
{
    private readonly Dictionary<string, TaskCompletionSource> _renderRequests = new();
    private Task? _initializeTask;
    private TaskCompletionSource? _readySource;
    private string _theme = "light";
    private string? _documentFolder;

    public PreviewHostView()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? LinkRequested;

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    public Task SetDocumentFolderAsync(string? folder)
    {
        _documentFolder = string.IsNullOrWhiteSpace(folder) ? null : folder;

        if (_initializeTask is not null)
        {
            MapDocumentFolder();
        }

        return Task.CompletedTask;
    }

    public async Task RenderAsync(string html, string theme, string renderId)
    {
        await InitializeAsync();

        _theme = AppThemeCatalog.FromWebTheme(theme).WebTheme;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderRequests[renderId] = completion;

        Post(new
        {
            type = "render",
            html,
            theme = _theme,
            id = renderId,
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            _renderRequests.Remove(renderId);
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        _theme = AppThemeCatalog.FromWebTheme(theme).WebTheme;

        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "theme",
            theme = _theme,
        });
    }

    public async Task SetScrollPercentAsync(double percent)
    {
        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "scroll",
            percent,
        });
    }

    public async Task PrintToPdfAsync(string outputPath)
    {
        await InitializeAsync();

        var core = WebView.CoreWebView2;
        var settings = core.Environment.CreatePrintSettings();
        settings.PageWidth = 8.27;
        settings.PageHeight = 11.69;
        settings.MarginTop = 0.71;
        settings.MarginBottom = 0.63;
        settings.MarginLeft = 0.63;
        settings.MarginRight = 0.63;
        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintHeaderAndFooter = false;
        settings.Orientation = CoreWebView2PrintOrientation.Portrait;

        var success = await core.PrintToPdfAsync(outputPath, settings);
        if (!success)
        {
            throw new InvalidOperationException("PDF 輸出失敗。");
        }
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            _readySource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await WaitForLoadedAsync();
            await WebView.EnsureCoreWebView2Async();

            var core = WebView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            SetBackground(_theme);
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationStarting += OnNavigationStarting;

            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            core.SetVirtualHostNameToFolderMapping(
                "app.local",
                assets,
                CoreWebView2HostResourceAccessKind.Allow);
            MapDocumentFolder(core);

            WebView.Source = new Uri("https://app.local/Preview/preview.html");
            await _readySource.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Post(new { type = "theme", theme = _theme });
        }
        catch (Exception ex)
        {
            ShowError(
                "請確認已安裝 Microsoft Edge WebView2 Runtime，且應用程式 Assets 資料夾完整。\n\n" +
                ex.Message);
            throw new InvalidOperationException("預覽初始化失敗。", ex);
        }
    }

    private Task WaitForLoadedAsync()
    {
        if (IsLoaded)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler handler = null!;
        handler = (_, _) =>
        {
            Loaded -= handler;
            completion.TrySetResult();
        };

        Loaded += handler;
        return completion.Task;
    }

    private void MapDocumentFolder()
    {
        var core = WebView.CoreWebView2;
        if (core is not null)
        {
            MapDocumentFolder(core);
        }
    }

    private void MapDocumentFolder(CoreWebView2 core)
    {
        var folder = _documentFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Preview");
        }

        try
        {
            core.ClearVirtualHostNameToFolderMapping("doc.local");
        }
        catch (Exception)
        {
        }

        core.SetVirtualHostNameToFolderMapping(
            "doc.local",
            folder,
            CoreWebView2HostResourceAccessKind.Allow);
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!args.Uri.StartsWith("https://app.local/", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = true;
        }
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty))
            {
                return;
            }

            switch (typeProperty.GetString())
            {
                case "ready":
                    _readySource?.TrySetResult();
                    break;

                case "rendered":
                    {
                        var id = GetString(root, "id");
                        if (_renderRequests.TryGetValue(id, out var completion))
                        {
                            completion.TrySetResult();
                        }

                        break;
                    }

                case "link":
                    LinkRequested?.Invoke(this, GetString(root, "href"));
                    break;
            }
        }
        catch (JsonException ex)
        {
            ShowError("預覽訊息格式錯誤。\n\n" + ex.Message);
        }
    }

    private void Post(object message)
    {
        var core = WebView.CoreWebView2;
        if (core is null)
        {
            return;
        }

        core.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private void SetBackground(string theme)
    {
        WebView.DefaultBackgroundColor = AppThemeCatalog.FromWebTheme(theme).PreviewBackground;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
