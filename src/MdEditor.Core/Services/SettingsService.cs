using System.Text.Json;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    string SettingsPath { get; }

    bool IsLoaded { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsService(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        SettingsPath = Path.Combine(baseDirectory, "settings.json");
    }

    public AppSettings Settings { get; private set; } = new();

    public string SettingsPath { get; }

    public bool IsLoaded { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_baseDirectory);

        if (!File.Exists(SettingsPath))
        {
            Settings = new AppSettings();
            IsLoaded = true;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken).ConfigureAwait(false);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackupCorruptFile();
            Settings = new AppSettings();
        }

        Normalize(Settings);
        IsLoaded = true;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(_baseDirectory);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            var tempPath = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

                if (File.Exists(SettingsPath))
                {
                    File.Replace(tempPath, SettingsPath, null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, SettingsPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.RecentFiles ??= [];
        settings.Translation ??= new TranslationSettings();

        NormalizeThemeSettings(settings);
        NormalizeTranslationSettings(settings.Translation);

        if (settings.ViewMode is not ("Edit" or "Preview" or "Split"))
        {
            settings.ViewMode = "Split";
        }

        if (settings.WindowState is not ("Normal" or "Maximized"))
        {
            settings.WindowState = "Normal";
        }

        settings.EditorFontFamily = string.IsNullOrWhiteSpace(settings.EditorFontFamily)
            ? "Cascadia Mono"
            : settings.EditorFontFamily.Trim();
        settings.EditorFontSize = Math.Clamp(settings.EditorFontSize, 10, 28);
        settings.EditorSplitRatio = Math.Clamp(settings.EditorSplitRatio, 0.25, 0.75);
        settings.RecentFilesLimit = Math.Clamp(settings.RecentFilesLimit, 5, 100);
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 800, 10000);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight, 500, 10000);
    }

    private static void NormalizeTranslationSettings(TranslationSettings translation)
    {
        translation.Endpoint = string.IsNullOrWhiteSpace(translation.Endpoint)
            ? TranslationSettings.DefaultEndpoint
            : translation.Endpoint.Trim();

        if (!Uri.TryCreate(translation.Endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttps &&
             !(endpointUri.Scheme == Uri.UriSchemeHttp && endpointUri.IsLoopback)))
        {
            translation.Endpoint = TranslationSettings.DefaultEndpoint;
        }

        translation.Model = translation.Model?.Trim() ?? string.Empty;
        translation.SourceLanguage = TranslationLanguageCatalog.ResolveSource(translation.SourceLanguage);
        translation.TargetLanguage = TranslationLanguageCatalog.ResolveTarget(translation.TargetLanguage);
        translation.MaxCharactersPerRequest = Math.Clamp(translation.MaxCharactersPerRequest, 500, 20000);
        translation.Temperature = Math.Clamp(translation.Temperature, 0, 1);
        translation.RequestTimeoutSeconds = Math.Clamp(translation.RequestTimeoutSeconds, 20, 600);
    }

    private static void NormalizeThemeSettings(AppSettings settings)
    {
        switch (settings.Theme)
        {
            case "System" or "Light" or "Dark":
                break;

            case "Sepia" or "Morning" or "Lavender":
                settings.ThemeColor = settings.Theme;
                settings.Theme = "Light";
                break;

            case "Nord" or "Forest":
                settings.ThemeColor = settings.Theme;
                settings.Theme = "Dark";
                break;

            case "HighContrast":
                settings.Theme = "Dark";
                break;

            default:
                settings.Theme = "System";
                break;
        }

        if (settings.ThemeColor is not ("Sepia" or "Morning" or "Lavender" or "Forest" or "Nord" or "Rose"))
        {
            settings.ThemeColor = "Sepia";
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backup = SettingsPath + ".corrupt-" + DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
            File.Copy(SettingsPath, backup, overwrite: true);
        }
        catch (Exception)
        {
        }
    }
}
