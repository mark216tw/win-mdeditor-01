using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Core.Helpers;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VibeCode.MdEditor.Services;

public enum SaveDecision
{
    Save,
    Discard,
    Cancel,
}

public enum SaveConflictDecision
{
    Overwrite,
    SaveAs,
    Cancel,
}

public interface IDialogService
{
    bool IsDialogOpen { get; }

    void Initialize(Window window);

    Task<string?> PickOpenFileAsync();

    Task<string?> PickSaveFileAsync(string suggestedName, string typeLabel, string extension);

    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog);

    Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText);

    Task<SaveDecision> ConfirmSaveAsync(string title, string message);

    Task<SaveConflictDecision> ConfirmSaveConflictAsync(string title, string message);

    Task ShowMessageAsync(string title, string message);
}

public sealed class DialogService : IDialogService, IDisposable
{
    private readonly SemaphoreSlim _dialogLock = new(1, 1);
    private Window? _window;
    private bool _disposed;

    public bool IsDialogOpen => _dialogLock.CurrentCount == 0;

    public void Initialize(Window window)
    {
        _window = window;
    }

    public async Task<string?> PickOpenFileAsync()
    {
        var window = RequireWindow();
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };

        foreach (var extension in SupportedFileTypes.Extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName, string typeLabel, string extension)
    {
        var window = RequireWindow();
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = SanitizeFileName(suggestedName),
            DefaultFileExtension = extension,
        };

        picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        dialog.XamlRoot ??= RequireXamlRoot();
        if (dialog.RequestedTheme == ElementTheme.Default)
        {
            dialog.RequestedTheme = ResolveRequestedTheme();
        }

        await _dialogLock.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            RequestedTheme = ResolveRequestedTheme(),
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Primary,
        };

        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task<SaveDecision> ConfirmSaveAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            RequestedTheme = ResolveRequestedTheme(),
            Title = title,
            Content = message,
            PrimaryButtonText = "儲存",
            SecondaryButtonText = "不儲存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await ShowDialogAsync(dialog) switch
        {
            ContentDialogResult.Primary => SaveDecision.Save,
            ContentDialogResult.Secondary => SaveDecision.Discard,
            _ => SaveDecision.Cancel,
        };
    }

    public async Task<SaveConflictDecision> ConfirmSaveConflictAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            RequestedTheme = ResolveRequestedTheme(),
            Title = title,
            Content = message,
            PrimaryButtonText = "覆蓋儲存",
            SecondaryButtonText = "另存新檔",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await ShowDialogAsync(dialog) switch
        {
            ContentDialogResult.Primary => SaveConflictDecision.Overwrite,
            ContentDialogResult.Secondary => SaveConflictDecision.SaveAs,
            _ => SaveConflictDecision.Cancel,
        };
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            RequestedTheme = ResolveRequestedTheme(),
            Title = title,
            Content = message,
            CloseButtonText = "關閉",
            DefaultButton = ContentDialogButton.Close,
        };

        await ShowDialogAsync(dialog);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dialogLock.Dispose();
    }

    private Window RequireWindow()
    {
        return _window ?? throw new InvalidOperationException("視窗尚未初始化。");
    }

    private XamlRoot RequireXamlRoot()
    {
        var content = RequireWindow().Content;
        return content?.XamlRoot ?? throw new InvalidOperationException("視窗內容尚未載入。");
    }

    private ElementTheme ResolveRequestedTheme()
    {
        return (RequireWindow().Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);

        foreach (var character in name)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "未命名" : result;
    }
}
