using System.Text.Json.Serialization;

namespace VibeCode.MdEditor.Core.Models;

public sealed class RecentFileEntry
{
    public string Path { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset LastOpenedUtc { get; set; }

    public bool IsPinned { get; set; }

    [JsonIgnore]
    public bool IsMissing { get; set; }
}
