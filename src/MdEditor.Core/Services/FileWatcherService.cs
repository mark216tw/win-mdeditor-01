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
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.OrdinalIgnoreCase);
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
            if (_registrations.ContainsKey(fullPath))
            {
                return;
            }

            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite |
                               NotifyFilters.Size |
                               NotifyFilters.FileName |
                               NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
            };

            var registration = new Registration(fullPath, watcher);
            watcher.Changed += (_, _) => Queue(registration, FileChangeKind.Changed);
            watcher.Created += (_, _) => Queue(registration, FileChangeKind.Created);
            watcher.Deleted += (_, _) => Queue(registration, FileChangeKind.Deleted);
            watcher.Renamed += (_, _) => Queue(registration, FileChangeKind.Renamed);
            watcher.Error += (_, _) => Queue(registration, FileChangeKind.Changed);

            registration.Timer = new Timer(
                OnTimer,
                registration,
                Timeout.Infinite,
                Timeout.Infinite);

            watcher.EnableRaisingEvents = true;
            _registrations[fullPath] = registration;
        }
    }

    public void Unwatch(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Registration? registration;

        lock (_sync)
        {
            if (!_registrations.Remove(fullPath, out registration))
            {
                return;
            }
        }

        DisposeRegistration(registration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Registration[] registrations;

        lock (_sync)
        {
            registrations = [.. _registrations.Values];
            _registrations.Clear();
        }

        foreach (var registration in registrations)
        {
            DisposeRegistration(registration);
        }

        GC.SuppressFinalize(this);
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
        lock (registration.Sync)
        {
            kind = registration.PendingKind;
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
        try
        {
            registration.Watcher.EnableRaisingEvents = false;
            registration.Watcher.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            registration.Timer?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private sealed class Registration(string path, FileSystemWatcher watcher)
    {
        public string Path { get; } = path;

        public FileSystemWatcher Watcher { get; } = watcher;

        public Lock Sync { get; } = new();

        public FileChangeKind PendingKind { get; set; } = FileChangeKind.Changed;

        public Timer? Timer { get; set; }
    }
}
