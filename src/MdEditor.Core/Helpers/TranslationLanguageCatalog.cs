using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Helpers;

public sealed record TranslationLanguageOption(string Code, string DisplayName);

public static class TranslationLanguageCatalog
{
    private static readonly TranslationLanguageOption[] SupportedLanguages =
    [
        new("zh-Hant", "繁體中文"),
        new("zh-Hans", "简体中文"),
        new("en", "English"),
        new("ja", "日本語"),
        new("ko", "한국어"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new("pt", "Português"),
        new("it", "Italiano"),
        new("ru", "Русский"),
        new("ar", "العربية"),
        new("th", "ไทย"),
        new("vi", "Tiếng Việt"),
        new("id", "Bahasa Indonesia"),
        new("tr", "Türkçe"),
        new("nl", "Nederlands"),
        new("pl", "Polski"),
        new("uk", "Українська"),
    ];

    public static IReadOnlyList<TranslationLanguageOption> TargetLanguages => SupportedLanguages;

    public static IReadOnlyList<TranslationLanguageOption> SourceLanguages { get; } =
    [
        new(TranslationSettings.AutoDetectSourceLanguage, "自動偵測"),
        .. SupportedLanguages,
    ];

    public static bool IsSupportedSource(string? code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
               SourceLanguages.Any(option => string.Equals(option.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSupportedTarget(string? code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
               SupportedLanguages.Any(option => string.Equals(option.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveSource(string? code)
    {
        return IsSupportedSource(code) ? code!.Trim() : TranslationSettings.AutoDetectSourceLanguage;
    }

    public static string ResolveTarget(string? code)
    {
        return IsSupportedTarget(code) ? code!.Trim() : TranslationSettings.DefaultTargetLanguage;
    }

    public static string GetDisplayName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return TranslationSettings.DefaultTargetLanguage;
        }

        var match = SourceLanguages.FirstOrDefault(
            option => string.Equals(option.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

        return match?.DisplayName ?? code.Trim();
    }
}
