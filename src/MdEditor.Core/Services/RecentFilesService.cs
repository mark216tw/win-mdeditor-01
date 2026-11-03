using System.Collections.ObjectModel;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public interface IRecentFilesService
{
    ObservableCollection<RecentFileEntry> Items { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(string path, CancellationToken cancellationToken = default);

    Task RemoveAsync(string path, CancellationToken cancellationToken = default);

    Task TogglePinAsync(string path, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task PruneAsync(CancellationToken cancellationToken = default);
}

public sealed class RecentFilesService : IRecentFilesService
{
    private readonly ISettingsService _settingsService;

    public RecentFilesService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ObservableCollection<RecentFileEntry> Items { get; } = [];

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Rebuild();
        return Task.CompletedTask;
    }

    public async Task AddOrUpdateAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!SupportedFileTypes.IsSupported(fullPath))
        {
            return;
        }

        var entries = _settingsService.Settings.RecentFiles;
        var entry = entries.FirstOrDefault(e => e.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            entry = new RecentFileEntry
            {
                Path = fullPath,
                DisplayName = Path.GetFileName(fullPath),
            };
            entries.Add(entry);
        }

        entry.DisplayName = Path.GetFileName(fullPath);
        entry.LastOpenedUtc = DateTimeOffset.UtcNow;
        entry.IsMissing = false;

        Trim(entries);
        Rebuild();
        await _settingsService.SaveAsync(cancellationToken);
    }

    public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var entries = _settingsService.Settings.RecentFiles;
        entries.RemoveAll(e => e.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        Rebuild();
        await _settingsService.SaveAsync(cancellationToken);
    }

    public async Task TogglePinAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var entry = _settingsService.Settings.RecentFiles
            .FirstOrDefault(e => e.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return;
        }

        entry.IsPinned = !entry.IsPinned;
        Rebuild();
        await _settingsService.SaveAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _settingsService.Settings.RecentFiles.Clear();
        Rebuild();
        await _settingsService.SaveAsync(cancellationToken);
    }

    public async Task PruneAsync(CancellationToken cancellationToken = default)
    {
        var entries = _settingsService.Settings.RecentFiles;
        var removedUnsupported = entries.RemoveAll(e => !SupportedFileTypes.IsSupported(e.Path)) > 0;
        var snapshot = entries.Select(e => e.Path).ToArray();

        var missing = await Task.Run(
            () => snapshot
                .Select(path => (Path: path, Exists: SafeExists(path)))
                .Where(item => !item.Exists)
                .Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        foreach (var entry in entries)
        {
            entry.IsMissing = missing.Contains(entry.Path);
        }

        Rebuild();

        if (removedUnsupported)
        {
            await _settingsService.SaveAsync(cancellationToken);
        }
    }

    private static bool SafeExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void Rebuild()
    {
        Items.Clear();
        foreach (var entry in Sort(_settingsService.Settings.RecentFiles))
        {
            Items.Add(entry);
        }
    }

    private static IEnumerable<RecentFileEntry> Sort(IEnumerable<RecentFileEntry> entries)
    {
        return entries
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.LastOpenedUtc)
            .ThenBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase);
    }

    private void Trim(List<RecentFileEntry> entries)
    {
        var limit = _settingsService.Settings.RecentFilesLimit;
        var pinned = entries.Where(e => e.IsPinned).ToList();
        var unpinned = entries
            .Where(e => !e.IsPinned)
            .OrderByDescending(e => e.LastOpenedUtc)
            .Take(Math.Max(0, limit - pinned.Count))
            .ToList();

        var keep = pinned.Concat(unpinned).ToHashSet();
        entries.RemoveAll(e => !keep.Contains(e));
    }
}
