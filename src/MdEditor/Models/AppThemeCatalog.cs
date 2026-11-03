using Windows.UI;

namespace VibeCode.MdEditor.Models;

public sealed record AppThemeDefinition(
    string Id,
    string DisplayName,
    bool IsDark,
    string WebTheme,
    string? ResourceUri,
    Color EditorBackground,
    Color PreviewBackground);

public static class AppThemeCatalog
{
    public static readonly AppThemeDefinition SystemTheme = new(
        "System",
        "跟隨系統",
        false,
        "system",
        null,
        Color.FromArgb(255, 255, 255, 255),
        Color.FromArgb(255, 255, 255, 255));

    public static readonly AppThemeDefinition LightTheme = new(
        "Light",
        "淺色",
        false,
        "light",
        null,
        Color.FromArgb(255, 255, 255, 255),
        Color.FromArgb(255, 255, 255, 255));

    public static readonly AppThemeDefinition DarkTheme = new(
        "Dark",
        "深色",
        true,
        "dark",
        null,
        Color.FromArgb(255, 30, 30, 30),
        Color.FromArgb(255, 13, 17, 23));

    public static readonly AppThemeDefinition SepiaTheme = new(
        "Sepia",
        "護眼",
        false,
        "sepia",
        "ms-appx:///Styles/Themes/Sepia.xaml",
        Color.FromArgb(255, 244, 236, 216),
        Color.FromArgb(255, 244, 236, 216));

    public static readonly AppThemeDefinition MorningTheme = new(
        "Morning",
        "晨光",
        false,
        "morning",
        "ms-appx:///Styles/Themes/Morning.xaml",
        Color.FromArgb(255, 245, 248, 253),
        Color.FromArgb(255, 245, 248, 253));

    public static readonly AppThemeDefinition LavenderTheme = new(
        "Lavender",
        "薰衣草",
        false,
        "lavender",
        "ms-appx:///Styles/Themes/Lavender.xaml",
        Color.FromArgb(255, 247, 244, 252),
        Color.FromArgb(255, 247, 244, 252));

    public static readonly AppThemeDefinition NordTheme = new(
        "Nord",
        "北歐藍",
        true,
        "nord",
        "ms-appx:///Styles/Themes/Nord.xaml",
        Color.FromArgb(255, 46, 52, 64),
        Color.FromArgb(255, 46, 52, 64));

    public static readonly AppThemeDefinition ForestTheme = new(
        "Forest",
        "森林綠",
        true,
        "forest",
        "ms-appx:///Styles/Themes/Forest.xaml",
        Color.FromArgb(255, 27, 42, 33),
        Color.FromArgb(255, 27, 42, 33));

    public static readonly AppThemeDefinition HighContrastTheme = new(
        "HighContrast",
        "高對比",
        true,
        "highcontrast",
        "ms-appx:///Styles/Themes/HighContrast.xaml",
        Color.FromArgb(255, 0, 0, 0),
        Color.FromArgb(255, 0, 0, 0));

    public static IReadOnlyList<AppThemeDefinition> All { get; } =
    [
        SystemTheme,
        LightTheme,
        DarkTheme,
        SepiaTheme,
        MorningTheme,
        LavenderTheme,
        NordTheme,
        ForestTheme,
        HighContrastTheme,
    ];

    public static AppThemeDefinition Resolve(string? id, bool systemIsDark)
    {
        var theme = All.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        if (theme is null || ReferenceEquals(theme, SystemTheme))
        {
            return systemIsDark ? DarkTheme : LightTheme;
        }

        return theme;
    }

    public static AppThemeDefinition FromWebTheme(string? webTheme)
    {
        return All.FirstOrDefault(item =>
            !ReferenceEquals(item, SystemTheme) &&
            string.Equals(item.WebTheme, webTheme, StringComparison.OrdinalIgnoreCase))
            ?? LightTheme;
    }
}
