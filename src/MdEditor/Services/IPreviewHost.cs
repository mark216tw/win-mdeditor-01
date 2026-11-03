namespace VibeCode.MdEditor.Services;

public interface IPreviewHost
{
    event EventHandler<string>? LinkRequested;

    Task InitializeAsync();

    Task SetDocumentFolderAsync(string? folder);

    Task RenderAsync(string html, string theme, string renderId);

    Task SetThemeAsync(string theme);

    Task SetScrollPercentAsync(double percent);

    Task PrintToPdfAsync(string outputPath);
}
