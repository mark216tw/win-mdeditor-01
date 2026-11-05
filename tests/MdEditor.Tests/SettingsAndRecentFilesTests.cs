using VibeCode.MdEditor.Core.Models;
using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class SettingsAndRecentFilesTests
{
    [Fact]
    public void Settings_Defaults_UseFollowSystemAndSmallerWindow()
    {
        var settings = new AppSettings();

        Assert.Equal("System", settings.Theme);
        Assert.Equal(1120, settings.WindowWidth);
        Assert.Equal(720, settings.WindowHeight);
        Assert.Equal("Normal", settings.WindowState);
        Assert.Equal(0.5, settings.EditorSplitRatio);
        Assert.Equal("Cascadia Mono", settings.EditorFontFamily);
    }

    [Fact]
    public async Task Settings_SplitRatio_RoundTrips()
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.EditorSplitRatio = 0.62;
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal(0.62, second.Settings.EditorSplitRatio, 3);
    }

    [Fact]
    public async Task Settings_InvalidSplitRatio_IsClamped()
    {
        using var temp = new TempDirectory();
        var settingsPath = Path.Combine(temp.DirectoryPath, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """{"EditorSplitRatio":0.1}""");

        var settings = new SettingsService(temp.DirectoryPath);
        await settings.LoadAsync();

        Assert.Equal(0.25, settings.Settings.EditorSplitRatio, 3);
    }

    [Fact]
    public async Task Settings_BlankEditorFontFamily_FallsBackToDefault()
    {
        using var temp = new TempDirectory();
        var settingsPath = Path.Combine(temp.DirectoryPath, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """{"EditorFontFamily":"  "}""");

        var settings = new SettingsService(temp.DirectoryPath);
        await settings.LoadAsync();

        Assert.Equal("Cascadia Mono", settings.Settings.EditorFontFamily);
    }

    [Fact]
    public async Task Settings_RoundTrip_ThroughJsonFile()
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.Theme = "Dark";
        first.Settings.WordWrap = false;
        first.Settings.SyncScrolling = false;
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal("Dark", second.Settings.Theme);
        Assert.False(second.Settings.WordWrap);
        Assert.False(second.Settings.SyncScrolling);
    }

    [Fact]
    public async Task Settings_LegacyFileWithoutSyncScrolling_DefaultsToEnabled()
    {
        using var temp = new TempDirectory();
        var settingsPath = Path.Combine(temp.DirectoryPath, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """{"Theme":"Dark"}""");

        var settings = new SettingsService(temp.DirectoryPath);
        await settings.LoadAsync();

        Assert.Equal("Dark", settings.Settings.Theme);
        Assert.True(settings.Settings.SyncScrolling);
    }

    [Theory]
    [InlineData("Sepia")]
    [InlineData("Morning")]
    [InlineData("Lavender")]
    [InlineData("Nord")]
    [InlineData("Forest")]
    [InlineData("HighContrast")]
    public async Task Settings_NewThemes_RoundTrip(string theme)
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.Theme = theme;
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal(theme, second.Settings.Theme);
    }

    [Fact]
    public async Task Settings_InvalidTheme_FallsBackToSystem()
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.Theme = "Unknown";
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal("System", second.Settings.Theme);
    }

    [Fact]
    public async Task Settings_MaximizedWindowState_RoundTrips()
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.WindowState = "Maximized";
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal("Maximized", second.Settings.WindowState);
    }

    [Fact]
    public async Task Settings_InvalidWindowState_FallsBackToNormal()
    {
        using var temp = new TempDirectory();
        var first = new SettingsService(temp.DirectoryPath);
        await first.LoadAsync();
        first.Settings.WindowState = "FullScreen";
        await first.SaveAsync();

        var second = new SettingsService(temp.DirectoryPath);
        await second.LoadAsync();

        Assert.Equal("Normal", second.Settings.WindowState);
    }

    [Fact]
    public async Task RecentFiles_AddsPrunesPinsAndClears()
    {
        using var temp = new TempDirectory();
        var firstFile = temp.Combine("first.md");
        var secondFile = temp.Combine("second.md");
        await File.WriteAllTextAsync(firstFile, "one");
        await File.WriteAllTextAsync(secondFile, "two");

        var settings = new SettingsService(temp.DirectoryPath);
        await settings.LoadAsync();

        var recent = new RecentFilesService(settings);
        await recent.InitializeAsync();
        await recent.AddOrUpdateAsync(firstFile);
        await recent.AddOrUpdateAsync(secondFile);

        Assert.Equal(2, recent.Items.Count);
        Assert.Equal(Path.GetFileName(secondFile), recent.Items[0].DisplayName);

        File.Delete(firstFile);
        await recent.PruneAsync();

        var missing = recent.Items.Single(item =>
            item.Path.Equals(Path.GetFullPath(firstFile), StringComparison.OrdinalIgnoreCase));
        Assert.True(missing.IsMissing);

        await recent.TogglePinAsync(secondFile);
        var pinned = recent.Items.Single(item =>
            item.Path.Equals(Path.GetFullPath(secondFile), StringComparison.OrdinalIgnoreCase));
        Assert.True(pinned.IsPinned);
        Assert.Same(pinned, recent.Items[0]);

        await recent.RemoveAsync(secondFile);
        Assert.Single(recent.Items);

        await recent.ClearAsync();
        Assert.Empty(recent.Items);
    }
}
