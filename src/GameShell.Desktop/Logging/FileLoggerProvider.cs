using System.IO;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;

    public FileLoggerProvider()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameShell",
            "Logs");

        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, $"gameshell-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_logFilePath, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly Lock Gate = new();
        private readonly string _categoryName;
        private readonly string _logFilePath;

        public FileLogger(string logFilePath, string categoryName)
        {
            _logFilePath = logFilePath;
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
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (Gate)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
    }
}
