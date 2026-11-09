namespace VibeCode.MdEditor.Models;

public sealed record DocumentConflict(
    string DraftText,
    string DiskText,
    string DiskHash,
    DateTimeOffset DiskLastWriteTimeUtc,
    long DiskLength);
