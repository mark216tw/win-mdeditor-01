namespace VibeCode.MdEditor.Core.Models;

public sealed record LoadedDocument(
    string Text,
    string EncodingName,
    int CodePage,
    bool HasBom,
    string LineEnding,
    string Hash,
    DateTimeOffset LastWriteTimeUtc,
    long Length);
