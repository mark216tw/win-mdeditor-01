namespace VibeCode.MdEditor.Services;

public sealed record PreviewTaskToggledEventArgs(
    string DocumentId,
    int Line,
    bool IsChecked);
