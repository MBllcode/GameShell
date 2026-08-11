using System.IO;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    internal const long MaxFileBytes = 10 * 1024 * 1024;
    internal const int RetainedFileCount = 7;
    private readonly string? _logsDirectory;

    public FileLoggerProvider()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameShell",
            "Logs");

        try
        {
            Directory.CreateDirectory(logsDirectory);
            _logsDirectory = logsDirectory;
        }
        catch
        {
            _logsDirectory = null;
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_logsDirectory, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly Lock Gate = new();
        private readonly string _categoryName;
        private readonly string? _logsDirectory;

        public FileLogger(string? logsDirectory, string categoryName)
        {
            _logsDirectory = logsDirectory;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _logsDirectory is null)
            {
                return;
            }

            try
            {
                var line = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {formatter(state, exception)}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                lock (Gate)
                {
                    var path = GetWritablePath(_logsDirectory, DateTime.Now);
                    using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream);
                    writer.WriteLine(line);
                    CleanupOldFiles(_logsDirectory);
                }
            }
            catch
            {
                // Logging must never interrupt polling or application work.
            }
        }

        private static string GetWritablePath(string directory, DateTime now)
        {
            var prefix = $"gameshell-{now:yyyy-MM-dd}";
            var path = Path.Combine(directory, $"{prefix}.log");
            if (!File.Exists(path) || new FileInfo(path).Length < MaxFileBytes)
            {
                return path;
            }

            for (var index = 1; ; index++)
            {
                var rolledPath = Path.Combine(directory, $"{prefix}.{index}.log");
                if (!File.Exists(rolledPath) || new FileInfo(rolledPath).Length < MaxFileBytes)
                {
                    return rolledPath;
                }
            }
        }

        private static void CleanupOldFiles(string directory)
        {
            var files = Directory.GetFiles(directory, "gameshell-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
            foreach (var file in files.Skip(RetainedFileCount))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
