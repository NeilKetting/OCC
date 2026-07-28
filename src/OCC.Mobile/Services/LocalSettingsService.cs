using System;
using System.IO;
using System.Text.Json;

using System.ComponentModel;

namespace OCC.Mobile.Services
{
    public enum AppEnvironment
    {
        [Description("Live")]
        Live,
        [Description("Test")]
        Test,
        [Description("Local-PC")]
        LocalPC,
        [Description("Local-Laptop")]
        LocalLaptop
    }

    public static class AppEnvironmentExtensions
    {
        public static bool IsLocal(this AppEnvironment environment)
        {
            return environment == AppEnvironment.LocalPC || environment == AppEnvironment.LocalLaptop;
        }
    }

    public class LocalSettings
    {
        public string LastEmail { get; set; } = string.Empty;
        public bool RememberEmail { get; set; } = true;
        public AppEnvironment SelectedEnvironment { get; set; } = AppEnvironment.Test;
        public string? CustomLocalUrl { get; set; } = string.Empty;

        // Cached statistics for unauthenticated login screen
        public int CachedActiveProjects { get; set; } = 0;
        public int CachedTasksToday { get; set; } = 0;
        public int CachedLiveSites { get; set; } = 0;
        public int CachedTeamMembers { get; set; } = 0;
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
            return new LocalSettings { SelectedEnvironment = AppEnvironment.Test };
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
