using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace VibeCode.MdEditor.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int MaxQueuedEntries = 4096;
    private const int RetentionDays = 14;

    private readonly string _directory;
    private readonly Channel<string> _channel;
    private readonly Task _writerTask;
    private volatile bool _disposed;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        PruneOldLogs(directory);

        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxQueuedEntries)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _writerTask = Task.Run(WriteLoopAsync);
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _channel.Writer.TryComplete();

        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
        }

        GC.SuppressFinalize(this);
    }

    private static void PruneOldLogs(string directory)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

            foreach (var path in Directory.EnumerateFiles(directory, "log-*.txt"))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        _channel.Writer.TryComplete();

        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
        }
    }

    private bool TryWrite(string line)
    {
        return !_disposed && _channel.Writer.TryWrite(line);
    }

    private async Task WriteLoopAsync()
    {
        StreamWriter? writer = null;
        string? currentDate = null;

        try
        {
            await foreach (var line in _channel.Reader.ReadAllAsync())
            {
                var date = DateTime.Now.ToString("yyyyMMdd");
                if (writer is null || !string.Equals(date, currentDate, StringComparison.Ordinal))
                {
                    if (writer is not null)
                    {
                        await writer.DisposeAsync();
                        writer = null;
                    }

                    currentDate = date;
                    var path = Path.Combine(_directory, $"log-{date}.txt");
                    var stream = new FileStream(
                        path,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite,
                        4096,
                        FileOptions.Asynchronous);

                    writer = new StreamWriter(stream, Encoding.UTF8)
                    {
                        AutoFlush = true,
                    };
                }

                await writer.WriteLineAsync(line);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.Append(" [");
            builder.Append(logLevel);
            builder.Append("] ");
            builder.Append(category);
            builder.Append(": ");
            builder.Append(formatter(state, exception));

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            builder.AppendLine();
            provider.TryWrite(builder.ToString());
        }
    }
}
