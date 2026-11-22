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
    DateTimeOffset LastWriteTimeUtc,
    long Revision,
    bool PersistText,
    double ScrollLine = 0,
    double ScrollPercent = 0,
    double PreviewScrollLine = 0,
    double PreviewScrollPercent = 0);
