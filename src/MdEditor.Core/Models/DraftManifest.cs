namespace VibeCode.MdEditor.Core.Models;

public sealed class DraftManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string? ActiveDocumentId { get; set; }

    public string ViewMode { get; set; } = "Split";

    public List<DraftManifestEntry> Documents { get; set; } = [];
}
