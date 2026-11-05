using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VibeCode.MdEditor.Models;
using VibeCode.MdEditor.Services;

namespace VibeCode.MdEditor.Views;

public sealed partial class EditorHostView : UserControl, IEditorHost
{
    private readonly Dictionary<string, TaskCompletionSource<string>> _contentRequests = new();
    private readonly Dictionary<string, TaskCompletionSource> _setContentRequests = new();
    private Task? _initializeTask;
    private TaskCompletionSource? _readySource;
    private string _theme = "light";
    private bool _wordWrap = true;
    private double _fontSize = 14;
    private bool _includeTextInChange = true;

    public EditorHostView()
    {
        InitializeComponent();
    }

    public event EventHandler<EditorContentChangedEventArgs>? ContentChanged;

    public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<EditorScrollEventArgs>? ScrollChanged;

    public event EventHandler<string>? ShortcutRequested;

    public event EventHandler<double>? FontSizeChanged;

    public event EventHandler<EditorImagePasteEventArgs>? ImagePasted;

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    public async Task SetContentAsync(string text, string documentId)
    {
        await InitializeAsync();
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _setContentRequests[requestId] = completion;

        Post(new
        {
            type = "setContent",
            text,
            documentId,
            id = requestId,
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _setContentRequests.Remove(requestId);
        }
    }

    public async Task<string> GetContentAsync(string documentId)
    {
        await InitializeAsync();

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _contentRequests[requestId] = completion;

        Post(new
        {
            type = "getContent",
            id = requestId,
            documentId,
        });

        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _contentRequests.Remove(requestId);
        }
    }

    public async Task DisposeDocumentAsync(string documentId)
    {
        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "disposeDocument",
            documentId,
        });
    }

    public async Task InsertTextAsync(string documentId, string text)
    {
        await InitializeAsync();
        Post(new
        {
            type = "insertText",
            documentId,
            text,
        });
    }

    public async Task ToggleTaskLineAsync(string documentId, int line, bool isChecked)
    {
        await InitializeAsync();
        Post(new
        {
            type = "toggleTask",
            documentId,
            line,
            @checked = isChecked,
        });
    }

    public async Task ExecuteCommandAsync(string command)
    {
        await InitializeAsync();
        Post(new
        {
            type = "command",
            name = command,
        });
    }

    public async Task SetThemeAsync(string theme)
    {
        _theme = AppThemeCatalog.FromWebTheme(theme).WebTheme;
        SetBackground(_theme);

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

    public async Task SetWordWrapAsync(bool enabled)
    {
        _wordWrap = enabled;

        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "wordWrap",
            enabled,
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

    public async Task SetIncludeTextInChangeAsync(bool enabled)
    {
        _includeTextInChange = enabled;

        if (_initializeTask is null)
        {
            return;
        }

        await InitializeAsync();
        Post(new
        {
            type = "changeMode",
            includeText = _includeTextInChange,
        });
    }

    public async Task SetReadOnlyAsync(bool enabled)
    {
        await InitializeAsync();
        Post(new
        {
            type = "readOnly",
            enabled,
        });
    }

    public async Task FocusAsync()
    {
        await InitializeAsync();
        WebView.Focus(FocusState.Programmatic);
        Post(new { type = "focus" });
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
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationStarting += OnNavigationStarting;

            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            core.SetVirtualHostNameToFolderMapping(
                "app.local",
                assets,
                CoreWebView2HostResourceAccessKind.Allow);

            SetBackground(_theme);
            WebView.Source = new Uri("https://app.local/Editor/editor.html");

            await _readySource.Task.WaitAsync(TimeSpan.FromSeconds(15));
            LoadingPanel.Visibility = Visibility.Collapsed;

            Post(new { type = "theme", theme = _theme });
            Post(new { type = "wordWrap", enabled = _wordWrap });
            Post(new { type = "font", size = _fontSize });
            Post(new { type = "changeMode", includeText = _includeTextInChange });
        }
        catch (Exception ex)
        {
            ShowError(
                "請確認已安裝 Microsoft Edge WebView2 Runtime，且應用程式 Assets 資料夾完整。\n\n" +
                ex.Message);
            throw new InvalidOperationException("編輯器初始化失敗。", ex);
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

                case "change":
                    ContentChanged?.Invoke(this, new EditorContentChangedEventArgs(
                        GetString(root, "documentId"),
                        GetNullableString(root, "text")));
                    break;

                case "selection":
                    SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs(
                        GetString(root, "documentId"),
                        GetInt32(root, "line"),
                        GetInt32(root, "column"),
                        GetInt32(root, "selectionLength")));
                    break;

                case "scroll":
                    ScrollChanged?.Invoke(this, new EditorScrollEventArgs(
                        GetString(root, "documentId"),
                        GetDouble(root, "percent"),
                        GetDouble(root, "line")));
                    break;

                case "pasteImage":
                    ImagePasted?.Invoke(this, new EditorImagePasteEventArgs(
                        GetString(root, "documentId"),
                        GetString(root, "mimeType"),
                        GetString(root, "base64")));
                    break;

                case "shortcut":
                    ShortcutRequested?.Invoke(this, GetString(root, "name"));
                    break;

                case "fontSize":
                    FontSizeChanged?.Invoke(this, GetDouble(root, "size"));
                    break;

                case "content":
                    {
                        var requestId = GetString(root, "id");
                        if (_contentRequests.TryGetValue(requestId, out var completion))
                        {
                            completion.TrySetResult(GetString(root, "text"));
                        }

                        break;
                    }

                case "contentSet":
                    {
                        var requestId = GetString(root, "id");
                        if (_setContentRequests.TryGetValue(requestId, out var completion))
                        {
                            completion.TrySetResult();
                        }

                        break;
                    }
            }
        }
        catch (JsonException ex)
        {
            ShowError("編輯器訊息格式錯誤。\n\n" + ex.Message);
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
        WebView.DefaultBackgroundColor = AppThemeCatalog.FromWebTheme(theme).EditorBackground;
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

    private static string? GetNullableString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int GetInt32(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out var value)
            ? value
            : 0;
    }
}
