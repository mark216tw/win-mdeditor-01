namespace VibeCode.MdEditor.Services;

public interface IPreviewHost
{
    event EventHandler<string>? LinkRequested;

    event EventHandler<string>? ShortcutRequested;

    event EventHandler<double>? FontSizeChanged;

    event EventHandler<PreviewTaskToggledEventArgs>? TaskToggled;

    Task InitializeAsync();

    Task SetDocumentFolderAsync(string? folder);

    Task RenderAsync(string html, string theme, string renderId, string documentId);

    Task SetThemeAsync(string theme);

    Task SetFontSizeAsync(double size);

    Task SetScrollPercentAsync(double percent, double line = 0);

    Task PrintToPdfAsync(string outputPath);
}
