namespace VibeCode.MdEditor.Core.Models;

public sealed record DraftSession(
    string? ActiveDocumentId,
    string ViewMode,
    IReadOnlyList<DraftDocumentSnapshot> Documents);
