namespace VibeCode.MdEditor.Services;

public interface IPreviewHost
{
    event EventHandler<string>? LinkRequested;

    event EventHandler<double>? FontSizeChanged;

    Task InitializeAsync();

    Task SetDocumentFolderAsync(string? folder);

    Task RenderAsync(string html, string theme, string renderId, string documentId);

    Task SetThemeAsync(string theme);

    Task SetFontSizeAsync(double size);

    Task SetScrollPercentAsync(double percent);

    Task PrintToPdfAsync(string outputPath);
}
