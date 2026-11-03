using System.Text.Json;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    string SettingsPath { get; }

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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_baseDirectory);

        if (!File.Exists(SettingsPath))
        {
            Settings = new AppSettings();
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

        if (settings.Theme is not ("System" or "Light" or "Dark" or "Sepia" or "Morning" or "Lavender" or "Nord" or "Forest" or "HighContrast"))
        {
            settings.Theme = "System";
        }

        if (settings.ViewMode is not ("Edit" or "Preview" or "Split"))
        {
            settings.ViewMode = "Split";
        }

        if (settings.WindowState is not ("Normal" or "Maximized"))
        {
            settings.WindowState = "Normal";
        }

        settings.EditorFontSize = Math.Clamp(settings.EditorFontSize, 9, 32);
        settings.RecentFilesLimit = Math.Clamp(settings.RecentFilesLimit, 5, 100);
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 800, 10000);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight, 500, 10000);
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
