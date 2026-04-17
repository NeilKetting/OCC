using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Infrastructure
{
    public class UserPreferencesService
    {
        private const string FileName = "userpreferences.json";
        private readonly string _filePath;

        public UserPreferencesDetails Preferences { get; private set; } = new();

        public UserPreferencesService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "OCC.Client");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _filePath = Path.Combine(folder, FileName);
            LoadPreferences();
        }

        private void LoadPreferences()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var prefs = JsonSerializer.Deserialize<UserPreferencesDetails>(json);
                    if (prefs != null)
                    {
                        Preferences = prefs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load preferences: {ex.Message}");
            }
        }

        public void SavePreferences()
        {
            try
            {
                var json = JsonSerializer.Serialize(Preferences);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save preferences: {ex.Message}");
            }
        }
    }

    public class UserPreferencesDetails
    {
        public int SessionTimeoutMinutes { get; set; } = 5; // Default
        public int LastBirthdayWishYear { get; set; }

        // Calendar Filters
        public bool ShowTasks { get; set; } = true;
        public bool ShowMeetings { get; set; } = true;
        public bool ShowToDos { get; set; } = true;
        public bool ShowBirthdays { get; set; } = true;
        public bool ShowPublicHolidays { get; set; } = true;
        public bool ShowProjectMilestones { get; set; } = true;
        public bool ShowLeave { get; set; } = true;
        public bool ShowOrderDeliveries { get; set; } = true;
    }
}
