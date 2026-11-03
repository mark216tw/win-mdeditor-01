namespace VibeCode.MdEditor.Core.Models;

public sealed class DraftManifestEntry
{
    public string DocumentId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public bool IsDirty { get; set; }

    public string EncodingName { get; set; } = "UTF-8";

    public int CodePage { get; set; } = 65001;

    public bool HasBom { get; set; }

    public string LineEnding { get; set; } = "LF";

    public string LastKnownHash { get; set; } = string.Empty;

    public DateTimeOffset LastWriteTimeUtc { get; set; }
}
