using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;

namespace VibeCode.MdEditor.Views;

public sealed partial class PreviewHostView : UserControl, IPreviewHost
{
    private (string Id, TaskCompletionSource Completion)? _pendingRender;
    private TaskCompletionSource? _pendingPrint;
    private Task? _initializeTask;
    private TaskCompletionSource? _readySource;
    private string _theme = "light";
    private double _fontSize = 15;
    private string? _documentFolder;

    public PreviewHostView()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? LinkRequested;

    public event EventHandler<string>? ShortcutRequested;

    public event EventHandler<double>? FontSizeChanged;

    public event EventHandler<PreviewTaskToggledEventArgs>? TaskToggled;

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    public Task SetDocumentFolderAsync(string? folder)
    {
        var normalized = string.IsNullOrWhiteSpace(folder) ? null : folder;
        if (string.Equals(_documentFolder, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        _documentFolder = normalized;

        if (_initializeTask is not null)
        {
            MapDocumentFolder();
        }

        return Task.CompletedTask;
    }

    public async Task RenderAsync(string html, string theme, string renderId, string documentId)
    {
        await InitializeAsync();

        _theme = AppThemeCatalog.FromWebTheme(theme).WebTheme;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = _pendingRender;
        _pendingRender = (renderId, completion);
        previous?.Completion.TrySetResult();

        Post(new
        {
            type = "render",
            html,
            theme = _theme,
            id = renderId,
            documentId,
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            if (_pendingRender?.Completion == completion)
            {
                _pendingRender = null;
            }
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

    public async Task SetFontSizeAsync(double size)
    {
        _fontSize = Math.Clamp(size, 10, 28);

        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "font",
            size = _fontSize,
        });
    }

    public async Task SetScrollPercentAsync(double percent, double line = 0)
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
            line,
        });
    }

    public async Task PrintToPdfAsync(string outputPath)
    {
        await InitializeAsync();
        await PrepareForPrintAsync();

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

    private async Task PrepareForPrintAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPrint = completion;

        Post(new
        {
            type = "preparePrint",
            id = Guid.NewGuid().ToString("N"),
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            if (_pendingPrint == completion)
            {
                _pendingPrint = null;
            }
        }
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            _readySource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await WaitForLoadedAsync();
            var environment = await WebViewEnvironmentProvider.GetAsync();
            await WebView.EnsureCoreWebView2Async(environment);

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
            LoadingPanel.Visibility = Visibility.Collapsed;
            Post(new { type = "theme", theme = _theme });
            Post(new { type = "font", size = _fontSize });
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
                        var pending = _pendingRender;
                        if (pending is not null &&
                            string.Equals(pending.Value.Id, id, StringComparison.Ordinal))
                        {
                            pending.Value.Completion.TrySetResult();
                        }

                        break;
                    }

                case "printReady":
                    _pendingPrint?.TrySetResult();
                    break;

                case "link":
                    LinkRequested?.Invoke(this, GetString(root, "href"));
                    break;

                case "shortcut":
                    ShortcutRequested?.Invoke(this, GetString(root, "name"));
                    break;

                case "fontSize":
                    FontSizeChanged?.Invoke(this, GetDouble(root, "size"));
                    break;

                case "toggleTask":
                    TaskToggled?.Invoke(this, new PreviewTaskToggledEventArgs(
                        GetString(root, "documentId"),
                        GetInt32(root, "line"),
                        GetBoolean(root, "checked")));
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
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out var value)
            ? value
            : 0;
    }

    private static int GetInt32(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) &&
               (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False) &&
               property.GetBoolean();
    }
}
