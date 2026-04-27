using System;
using System.IO;
using System.Text.Json;

namespace OCC.Mobile.Services
{
    public enum AppEnvironment
    {
        Live,
        Local
    }

    public class LocalSettings
    {
        public string LastEmail { get; set; } = string.Empty;
        public bool RememberEmail { get; set; } = true;
        public AppEnvironment SelectedEnvironment { get; set; } = AppEnvironment.Local;
        public string? CustomLocalUrl { get; set; } = "http://192.168.0.191:5237";
    }

    public interface ILocalSettingsService
    {
        LocalSettings Settings { get; }
        void Save();
    }

    public class LocalSettingsService : ILocalSettingsService
    {
        private readonly string _filePath;
        private LocalSettings _settings;

        public LocalSettings Settings => _settings;

        public LocalSettingsService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC.Mobile");
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
            return new LocalSettings { SelectedEnvironment = AppEnvironment.Local };
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
