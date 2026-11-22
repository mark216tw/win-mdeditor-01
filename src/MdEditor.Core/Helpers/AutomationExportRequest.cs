namespace VibeCode.MdEditor.Core.Helpers;

public sealed record AutomationExportRequest(
    AutomationExportFormat Format,
    string InputPath,
    string OutputPath,
    string? Theme,
    bool Force);
