namespace VibeCode.MdEditor.Core.Models;

public sealed record SearchResultSummary(
    string DocumentId,
    int CurrentIndex,
    int TotalCount,
    bool IsValidRegex = true);
