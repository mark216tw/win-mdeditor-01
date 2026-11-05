namespace VibeCode.MdEditor.Core.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";

    public string ViewMode { get; set; } = "Split";

    public string EditorFontFamily { get; set; } = "Cascadia Mono";

    public double EditorFontSize { get; set; } = 14;

    public bool WordWrap { get; set; } = true;

    public bool SyncScrolling { get; set; } = true;

    public int RecentFilesLimit { get; set; } = 20;

    public double WindowWidth { get; set; } = 1120;

    public double WindowHeight { get; set; } = 720;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public string WindowState { get; set; } = "Normal";

    public double EditorSplitRatio { get; set; } = 0.5;

    public List<RecentFileEntry> RecentFiles { get; set; } = [];
}
