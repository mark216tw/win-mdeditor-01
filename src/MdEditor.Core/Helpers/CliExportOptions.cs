namespace VibeCode.MdEditor.Core.Helpers;

public sealed record CliExportOptions(
    CliExportFormat Format,
    string InputPath,
    string? OutputPath,
    string? Theme,
    bool Force,
    bool Quiet);
