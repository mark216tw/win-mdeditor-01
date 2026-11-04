namespace VibeCode.MdEditor.Core.Services;

public enum FileChangeKind
{
    Changed,
    Created,
    Deleted,
    Renamed,
}

public sealed class FileWatcherEventArgs(string path, FileChangeKind kind) : EventArgs
{
    public string Path { get; } = path;

    public FileChangeKind Kind { get; } = kind;
}

public interface IFileWatcherService : IDisposable
{
    event EventHandler<FileWatcherEventArgs>? FileChanged;

    void Watch(string path);

    void Unwatch(string path);
}

public sealed class FileWatcherService : IFileWatcherService
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, DirectoryWatch> _directories = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public event EventHandler<FileWatcherEventArgs>? FileChanged;

    public void Watch(string path)
    {
        if (_disposed)
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (!_directories.TryGetValue(directory, out var watch))
            {
                watch = new DirectoryWatch(directory);
                watch.Watcher.Changed += (_, args) => OnChanged(watch, args.FullPath, FileChangeKind.Changed);
                watch.Watcher.Created += (_, args) => OnChanged(watch, args.FullPath, FileChangeKind.Created);
                watch.Watcher.Deleted += (_, args) => OnChanged(watch, args.FullPath, FileChangeKind.Deleted);
                watch.Watcher.Renamed += (_, args) => OnRenamed(watch, args);
                watch.Watcher.Error += (_, _) => OnWatcherError(watch);
                _directories[directory] = watch;

                watch.Registrations[fullPath] = CreateRegistration(fullPath);
                watch.Watcher.EnableRaisingEvents = true;
                return;
            }

            if (watch.Registrations.ContainsKey(fullPath))
            {
                return;
            }

            watch.Registrations[fullPath] = CreateRegistration(fullPath);
        }
    }

    private Registration CreateRegistration(string path)
    {
        var registration = new Registration(path);
        registration.Timer = new Timer(OnTimer, registration, Timeout.Infinite, Timeout.Infinite);
        return registration;
    }

    public void Unwatch(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Registration? registration;
        DirectoryWatch? watch;
        var removeDirectory = false;

        lock (_sync)
        {
            if (!_directories.TryGetValue(directory, out watch))
            {
                return;
            }

            if (!watch.Registrations.Remove(fullPath, out registration))
            {
                return;
            }

            if (watch.Registrations.Count == 0)
            {
                _directories.Remove(directory);
                removeDirectory = true;
            }
        }

        DisposeRegistration(registration);

        if (removeDirectory)
        {
            watch.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DirectoryWatch[] watches;

        lock (_sync)
        {
            watches = [.. _directories.Values];
            _directories.Clear();
        }

        foreach (var watch in watches)
        {
            watch.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void OnChanged(DirectoryWatch watch, string fullPath, FileChangeKind kind)
    {
        Registration? registration;

        try
        {
            fullPath = Path.GetFullPath(fullPath);
        }
        catch (Exception)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            watch.Registrations.TryGetValue(fullPath, out registration);
        }

        if (registration is not null)
        {
            Queue(registration, kind);
        }
    }

    private void OnRenamed(DirectoryWatch watch, RenamedEventArgs args)
    {
        Registration? registration;

        try
        {
            var oldPath = Path.GetFullPath(args.OldFullPath);

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                watch.Registrations.TryGetValue(oldPath, out registration);
            }
        }
        catch (Exception)
        {
            return;
        }

        if (registration is not null)
        {
            Queue(registration, FileChangeKind.Renamed);
        }
    }

    private void OnWatcherError(DirectoryWatch watch)
    {
        Registration[] registrations;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            registrations = [.. watch.Registrations.Values];
        }

        foreach (var registration in registrations)
        {
            Queue(registration, FileChangeKind.Changed);
        }
    }

    private void Queue(Registration registration, FileChangeKind kind)
    {
        try
        {
            lock (registration.Sync)
            {
                registration.PendingKind = kind;
            }

            registration.Timer?.Change(500, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnTimer(object? state)
    {
        if (state is not Registration registration)
        {
            return;
        }

        FileChangeKind kind;
        bool disabled;
        lock (registration.Sync)
        {
            kind = registration.PendingKind;
            disabled = registration.Disabled;
        }

        if (disabled)
        {
            return;
        }

        try
        {
            FileChanged?.Invoke(this, new FileWatcherEventArgs(registration.Path, kind));
        }
        catch (Exception)
        {
        }
    }

    private static void DisposeRegistration(Registration registration)
    {
        lock (registration.Sync)
        {
            registration.Disabled = true;
        }

        try
        {
            registration.Timer?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private sealed class DirectoryWatch(string directory) : IDisposable
    {
        public FileSystemWatcher Watcher { get; } = new(directory)
        {
            NotifyFilter = NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.FileName |
                           NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
        };

        public Dictionary<string, Registration> Registrations { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            try
            {
                Watcher.EnableRaisingEvents = false;
                Watcher.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    private sealed class Registration(string path)
    {
        public string Path { get; } = path;

        public Lock Sync { get; } = new();

        public FileChangeKind PendingKind { get; set; } = FileChangeKind.Changed;

        public Timer? Timer { get; set; }

        public bool Disabled { get; set; }
    }
}
