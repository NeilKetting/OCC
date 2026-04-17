using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OCC.WpfClient.Services.Infrastructure.Logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        public FileLoggerProvider()
        {
            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OCC", "wpf-logs");

            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Purge old logs: Keep only top 2 most recent
            try
            {
                var files = new DirectoryInfo(logsDir).GetFiles("wpf-log-*.txt")
                                    .OrderByDescending(f => f.LastWriteTime)
                                    .Skip(2);

                foreach (var file in files)
                {
                    file.Delete();
                }
            }
            catch { /* Ignore purge errors */ }
            
            _filePath = Path.Combine(logsDir, $"wpf-log-{DateTime.Now:yyyyMMdd}.txt");
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath);
        }

        public void Dispose()
        {
        }
    }

    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddWpfFileLogger(this ILoggingBuilder builder)
        {
            builder.AddProvider(new FileLoggerProvider());
            return builder;
        }
    }
}
