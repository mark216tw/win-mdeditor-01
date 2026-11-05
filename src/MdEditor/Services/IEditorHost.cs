namespace VibeCode.MdEditor.Services;

public sealed record EditorContentChangedEventArgs(string DocumentId, string? Text);

public sealed record EditorSelectionChangedEventArgs(string DocumentId, int Line, int Column, int SelectionLength);

public sealed record EditorScrollEventArgs(string DocumentId, double Percent, double Line = 0);

public interface IEditorHost
{
    event EventHandler<EditorContentChangedEventArgs>? ContentChanged;

    event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

    event EventHandler<EditorScrollEventArgs>? ScrollChanged;

    event EventHandler<string>? ShortcutRequested;

    event EventHandler<double>? FontSizeChanged;

    event EventHandler<EditorImagePasteEventArgs>? ImagePasted;

    Task InitializeAsync();

    Task SetContentAsync(string text, string documentId);

    Task<string> GetContentAsync(string documentId);

    Task DisposeDocumentAsync(string documentId);

    Task InsertTextAsync(string documentId, string text);

    Task ToggleTaskLineAsync(string documentId, int line, bool isChecked);

    Task ExecuteCommandAsync(string command);

    Task SetThemeAsync(string theme);

    Task SetWordWrapAsync(bool enabled);

    Task SetFontSizeAsync(double size);

    Task SetIncludeTextInChangeAsync(bool enabled);

    Task SetReadOnlyAsync(bool enabled);

    Task FocusAsync();
}
