using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VibeCode.MdEditor.Services;

public enum SaveDecision
{
    Save,
    Discard,
    Cancel,
}

public interface IDialogService
{
    void Initialize(Window window);

    Task<string?> PickOpenFileAsync();

    Task<string?> PickSaveFileAsync(string suggestedName, string typeLabel, string extension);

    Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText);

    Task<SaveDecision> ConfirmSaveAsync(string title, string message);

    Task ShowMessageAsync(string title, string message);
}

public sealed class DialogService : IDialogService
{
    private Window? _window;

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

        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".txt");

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

    public async Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<SaveDecision> ConfirmSaveAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            Title = title,
            Content = message,
            PrimaryButtonText = "儲存",
            SecondaryButtonText = "不儲存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => SaveDecision.Save,
            ContentDialogResult.Secondary => SaveDecision.Discard,
            _ => SaveDecision.Cancel,
        };
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RequireXamlRoot(),
            Title = title,
            Content = message,
            CloseButtonText = "關閉",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
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
