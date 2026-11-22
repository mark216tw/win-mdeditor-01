namespace VibeCode.MdEditor.Core.Models;

public sealed class TranslationSettings
{
    public const string DefaultEndpoint = "https://api.openai.com/v1";

    public const string DefaultTargetLanguage = "zh-Hant";

    public const string AutoDetectSourceLanguage = "auto";

    public string Endpoint { get; set; } = DefaultEndpoint;

    public string Model { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = AutoDetectSourceLanguage;

    public string TargetLanguage { get; set; } = DefaultTargetLanguage;

    public int MaxCharactersPerRequest { get; set; } = 6000;

    public double Temperature { get; set; } = 0.2;

    public int RequestTimeoutSeconds { get; set; } = 120;
}
