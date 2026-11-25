namespace VibeCode.MdEditor.Core.Models;

public sealed record SearchOptions(
    string Query,
    bool CaseSensitive = false,
    bool WholeWord = false,
    bool UseRegex = false);
