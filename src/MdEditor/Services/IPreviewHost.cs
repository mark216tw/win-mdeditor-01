namespace VibeCode.MdEditor.Services;

public sealed record PreviewScrollEventArgs(string DocumentId, double Percent, double Line = 0);

public interface IPreviewHost
{
    event EventHandler<string>? LinkRequested;

    event EventHandler<string>? ShortcutRequested;

    event EventHandler<double>? FontSizeChanged;

    event EventHandler<PreviewTaskToggledEventArgs>? TaskToggled;

    event EventHandler<PreviewScrollEventArgs>? ScrollChanged;

    Task InitializeAsync();

    Task SetDocumentFolderAsync(string? folder);

    Task RenderAsync(
        string html,
        string theme,
        string renderId,
        string documentId,
        double previewScrollLine = 0,
        double previewScrollPercent = 0);

    Task<string> GetRenderedHtmlAsync();

    Task SetThemeAsync(string theme);

    Task SetFontSizeAsync(double size);

    Task SetScrollPercentAsync(double percent, double line = 0, string? documentId = null);

    Task PrintToPdfAsync(string outputPath);
}
