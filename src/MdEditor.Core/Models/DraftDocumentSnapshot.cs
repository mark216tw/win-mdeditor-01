namespace VibeCode.MdEditor.Core.Models;

public sealed record DraftDocumentSnapshot(
    string DocumentId,
    string Title,
    string? FilePath,
    string Text,
    string EncodingName,
    int CodePage,
    bool HasBom,
    string LineEnding,
    bool IsDirty,
    string LastKnownHash,
    DateTimeOffset LastWriteTimeUtc);
