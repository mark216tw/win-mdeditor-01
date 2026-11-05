namespace VibeCode.MdEditor.Services;

public sealed record EditorImagePasteEventArgs(
    string DocumentId,
    string MimeType,
    string Base64Data);
