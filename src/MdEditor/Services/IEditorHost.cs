namespace VibeCode.MdEditor.Services;

public sealed record EditorContentChangedEventArgs(string DocumentId, string? Text);

public sealed record EditorSelectionChangedEventArgs(string DocumentId, int Line, int Column, int SelectionLength);

public sealed record EditorScrollEventArgs(string DocumentId, double Percent);

public interface IEditorHost
{
    event EventHandler<EditorContentChangedEventArgs>? ContentChanged;

    event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

    event EventHandler<EditorScrollEventArgs>? ScrollChanged;

    event EventHandler<string>? ShortcutRequested;

    event EventHandler<double>? FontSizeChanged;

    Task InitializeAsync();

    Task SetContentAsync(string text, string documentId);

    Task<string> GetContentAsync(string documentId);

    Task ExecuteCommandAsync(string command);

    Task SetThemeAsync(string theme);

    Task SetWordWrapAsync(bool enabled);

    Task SetFontSizeAsync(double size);

    Task SetIncludeTextInChangeAsync(bool enabled);

    Task FocusAsync();
}
