using Windows.UI;

namespace VibeCode.MdEditor.Models;

public sealed record AppThemeDefinition(
    string Id,
    string ColorId,
    string DisplayName,
    bool IsDark,
    string WebTheme,
    string? ResourceUri,
    Color EditorBackground,
    Color PreviewBackground);

public static class AppThemeCatalog
{
    public static IReadOnlyList<(string Id, string DisplayName)> DisplayModes { get; } =
    [
        ("System", "跟隨系統"),
        ("Light", "淺色"),
        ("Dark", "深色"),
    ];

    public static IReadOnlyList<(string Id, string DisplayName)> ThemeColors { get; } =
    [
        ("Sepia", "護眼黃"),
        ("Morning", "晨光"),
        ("Lavender", "薰衣草"),
        ("Forest", "森林綠"),
        ("Nord", "北歐藍"),
        ("Rose", "玫瑰粉"),
    ];

    public static readonly AppThemeDefinition LightTheme = new(
        "Light",
        "Neutral",
        "中性（淺色）",
        false,
        "light",
        null,
        Color.FromArgb(255, 255, 255, 255),
        Color.FromArgb(255, 255, 255, 255));

    public static readonly AppThemeDefinition DarkTheme = new(
        "Dark",
        "Neutral",
        "中性（深色）",
        true,
        "dark",
        null,
        Color.FromArgb(255, 30, 30, 30),
        Color.FromArgb(255, 13, 17, 23));

    public static readonly AppThemeDefinition SepiaLightTheme = new(
        "SepiaLight",
        "Sepia",
        "護眼黃（淺色）",
        false,
        "sepia-light",
        "ms-appx:///Styles/Themes/SepiaLight.xaml",
        Color.FromArgb(255, 244, 236, 216),
        Color.FromArgb(255, 244, 236, 216));

    public static readonly AppThemeDefinition SepiaDarkTheme = new(
        "SepiaDark",
        "Sepia",
        "護眼黃（深色）",
        true,
        "sepia-dark",
        "ms-appx:///Styles/Themes/SepiaDark.xaml",
        Color.FromArgb(255, 38, 32, 24),
        Color.FromArgb(255, 38, 32, 24));

    public static readonly AppThemeDefinition MorningLightTheme = new(
        "MorningLight",
        "Morning",
        "晨光（淺色）",
        false,
        "morning-light",
        "ms-appx:///Styles/Themes/MorningLight.xaml",
        Color.FromArgb(255, 245, 248, 253),
        Color.FromArgb(255, 245, 248, 253));

    public static readonly AppThemeDefinition MorningDarkTheme = new(
        "MorningDark",
        "Morning",
        "晨光（深色）",
        true,
        "morning-dark",
        "ms-appx:///Styles/Themes/MorningDark.xaml",
        Color.FromArgb(255, 22, 30, 43),
        Color.FromArgb(255, 22, 30, 43));

    public static readonly AppThemeDefinition LavenderLightTheme = new(
        "LavenderLight",
        "Lavender",
        "薰衣草（淺色）",
        false,
        "lavender-light",
        "ms-appx:///Styles/Themes/LavenderLight.xaml",
        Color.FromArgb(255, 247, 244, 252),
        Color.FromArgb(255, 247, 244, 252));

    public static readonly AppThemeDefinition LavenderDarkTheme = new(
        "LavenderDark",
        "Lavender",
        "薰衣草（深色）",
        true,
        "lavender-dark",
        "ms-appx:///Styles/Themes/LavenderDark.xaml",
        Color.FromArgb(255, 32, 27, 45),
        Color.FromArgb(255, 32, 27, 45));

    public static readonly AppThemeDefinition ForestLightTheme = new(
        "ForestLight",
        "Forest",
        "森林綠（淺色）",
        false,
        "forest-light",
        "ms-appx:///Styles/Themes/ForestLight.xaml",
        Color.FromArgb(255, 242, 248, 244),
        Color.FromArgb(255, 242, 248, 244));

    public static readonly AppThemeDefinition ForestDarkTheme = new(
        "ForestDark",
        "Forest",
        "森林綠（深色）",
        true,
        "forest-dark",
        "ms-appx:///Styles/Themes/ForestDark.xaml",
        Color.FromArgb(255, 27, 42, 33),
        Color.FromArgb(255, 27, 42, 33));

    public static readonly AppThemeDefinition NordLightTheme = new(
        "NordLight",
        "Nord",
        "北歐藍（淺色）",
        false,
        "nord-light",
        "ms-appx:///Styles/Themes/NordLight.xaml",
        Color.FromArgb(255, 236, 239, 244),
        Color.FromArgb(255, 236, 239, 244));

    public static readonly AppThemeDefinition NordDarkTheme = new(
        "NordDark",
        "Nord",
        "北歐藍（深色）",
        true,
        "nord-dark",
        "ms-appx:///Styles/Themes/NordDark.xaml",
        Color.FromArgb(255, 46, 52, 64),
        Color.FromArgb(255, 46, 52, 64));

    public static readonly AppThemeDefinition RoseLightTheme = new(
        "RoseLight",
        "Rose",
        "玫瑰粉（淺色）",
        false,
        "rose-light",
        "ms-appx:///Styles/Themes/RoseLight.xaml",
        Color.FromArgb(255, 253, 244, 246),
        Color.FromArgb(255, 253, 244, 246));

    public static readonly AppThemeDefinition RoseDarkTheme = new(
        "RoseDark",
        "Rose",
        "玫瑰粉（深色）",
        true,
        "rose-dark",
        "ms-appx:///Styles/Themes/RoseDark.xaml",
        Color.FromArgb(255, 40, 29, 34),
        Color.FromArgb(255, 40, 29, 34));

    public static IReadOnlyList<AppThemeDefinition> All { get; } =
    [
        LightTheme,
        DarkTheme,
        SepiaLightTheme,
        SepiaDarkTheme,
        MorningLightTheme,
        MorningDarkTheme,
        LavenderLightTheme,
        LavenderDarkTheme,
        ForestLightTheme,
        ForestDarkTheme,
        NordLightTheme,
        NordDarkTheme,
        RoseLightTheme,
        RoseDarkTheme,
    ];

    public static AppThemeDefinition Resolve(string? displayMode, string? themeColor, bool systemIsDark)
    {
        var isDark = displayMode switch
        {
            not null when displayMode.Equals("Dark", StringComparison.OrdinalIgnoreCase) => true,
            not null when displayMode.Equals("Light", StringComparison.OrdinalIgnoreCase) => false,
            _ => systemIsDark,
        };

        var normalizedColor = string.IsNullOrWhiteSpace(themeColor) ? "Sepia" : themeColor.Trim();
        return All.FirstOrDefault(item =>
            item.IsDark == isDark &&
            string.Equals(item.ColorId, normalizedColor, StringComparison.OrdinalIgnoreCase))
            ?? (isDark ? SepiaDarkTheme : SepiaLightTheme);
    }

    public static AppThemeDefinition FromWebTheme(string? webTheme)
    {
        return All.FirstOrDefault(item =>
            string.Equals(item.WebTheme, webTheme, StringComparison.OrdinalIgnoreCase))
            ?? LightTheme;
    }
}
