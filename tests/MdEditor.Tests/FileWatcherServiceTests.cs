using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class FileWatcherServiceTests
{
    [Fact]
    public async Task Watch_RaisesFileChangedEvent()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("watched.md");
        await File.WriteAllTextAsync(path, "first");

        using var watcher = new FileWatcherService();
        var completion = new TaskCompletionSource<FileWatcherEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        watcher.FileChanged += (_, args) =>
        {
            if (args.Path.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                completion.TrySetResult(args);
            }
        };

        watcher.Watch(path);
        await File.WriteAllTextAsync(path, "second");

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(Path.GetFullPath(path), result.Path);
    }

    [Fact]
    public async Task Watch_TwoFilesInSameDirectory_RaisesEventForChangedFile()
    {
        using var temp = new TempDirectory();
        var first = temp.Combine("first.md");
        var second = temp.Combine("second.md");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");

        using var watcher = new FileWatcherService();
        var completion = new TaskCompletionSource<FileWatcherEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        watcher.FileChanged += (_, args) =>
        {
            if (args.Path.Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase))
            {
                completion.TrySetResult(args);
            }
        };

        watcher.Watch(first);
        watcher.Watch(second);
        await File.WriteAllTextAsync(second, "changed");

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(Path.GetFullPath(second), result.Path);
    }

    [Fact]
    public async Task Watch_OnDDrive_RaisesFileChangedEvent()
    {
        var directory = @"D:\TEMP\opencode\watchtest";
        if (!Directory.Exists(@"D:\"))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".md");
        await File.WriteAllTextAsync(path, "first");

        using var watcher = new FileWatcherService();
        var completion = new TaskCompletionSource<FileWatcherEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        watcher.FileChanged += (_, args) =>
        {
            if (args.Path.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                completion.TrySetResult(args);
            }
        };

        watcher.Watch(path);
        await File.WriteAllTextAsync(path, "second");

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(Path.GetFullPath(path), result.Path);
        File.Delete(path);
    }
}
