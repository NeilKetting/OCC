using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OCC.WpfClient.Services.Infrastructure.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private static readonly object _lock = new object();

        public FileLogger(string categoryName, string filePath)
        {
            _categoryName = categoryName;
            _filePath = filePath;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logRecord = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
            {
                logRecord += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.AppendAllText(_filePath, logRecord + Environment.NewLine);
                }
                catch
                {
                    // Fail silently for logging
                }
            }
        }
    }
}
