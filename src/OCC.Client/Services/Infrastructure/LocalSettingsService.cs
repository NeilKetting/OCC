using System;
using System.IO;
using System.Text.Json;

namespace OCC.Client.Services.Infrastructure
{
    public class LocalSettings
    {
        public string LastEmail { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = true;
        public string? CustomLocalUrl { get; set; }
    }

    public class LocalSettingsService
    {
        private readonly string _filePath;
        private LocalSettings _settings;

        public LocalSettings Settings => _settings;

        public LocalSettingsService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC.Client");
            _filePath = Path.Combine(folder, "settings.json");
            
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _settings = LoadSettings();
        }

        private LocalSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
                }
            }
            catch
            {
                // Ignore errors, start fresh
            }
            return new LocalSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
