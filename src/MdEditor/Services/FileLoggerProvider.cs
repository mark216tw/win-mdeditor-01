using System.Text;
using Microsoft.Extensions.Logging;

namespace VibeCode.MdEditor.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly Lock _sync = new();

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_directory, categoryName, _sync);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private sealed class FileLogger(string directory, string category, Lock sync) : ILogger
    {
        private readonly string _directory = directory;
        private readonly string _category = category;
        private readonly Lock _sync = sync;

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
            builder.Append(_category);
            builder.Append(": ");
            builder.Append(formatter(state, exception));

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            builder.AppendLine();

            try
            {
                var path = Path.Combine(_directory, $"log-{DateTime.Now:yyyyMMdd}.txt");
                lock (_sync)
                {
                    File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
