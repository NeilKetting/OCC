using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services.Infrastructure
{
    public class LocalSettings
    {
        public string LastEmail { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
        public int SessionTimeoutMinutes { get; set; } = 5;
        public bool MaximizeOverTaskbar { get; set; } = false;
        public double ThemeBrightness { get; set; } = 0.5;
        public bool UsePlainMenuIcons { get; set; } = false;
        public bool KeepSidebarExpanded { get; set; } = false;
        public bool AutoCheckUpdates { get; set; } = true;
        public int FailedUpdateAttemptCount { get; set; } = 0;
        public string LastAttemptedUpdateVersion { get; set; } = string.Empty;
        public OCC.WpfClient.Infrastructure.Models.ListLayout? EmployeeListLayout { get; set; }

        // Layouts for other List Views
        public OCC.WpfClient.Infrastructure.Models.ListLayout? UserListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? CustomerListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? InventoryListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? PurchaseOrderListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? SupplierListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? ProjectListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? ProjectTaskListLayout { get; set; }
        public OCC.WpfClient.Infrastructure.Models.ListLayout? SubContractorListLayout { get; set; }

        // Calendar settings
        public bool CalendarShowTasks { get; set; } = true;
        public bool CalendarShowPublicHolidays { get; set; } = true;
        public bool CalendarShowBirthdays { get; set; } = true;
        public bool CalendarShowLeave { get; set; } = true;
        public bool CalendarShowProcurement { get; set; } = true;
        public System.Collections.Generic.List<Guid>? CalendarSelectedProjectIds { get; set; }
        public System.Collections.Generic.List<string>? QuickActions { get; set; }
        public System.Collections.Generic.Dictionary<string, bool>? WageRunVisibleColumns { get; set; }
        public System.Collections.Generic.List<OCC.WpfClient.Features.Main.Models.WidgetConfig>? DashboardWidgets { get; set; }
        public bool DisableOutlookSync { get; set; } = false;
        public bool MuteOutlookReminders { get; set; } = false;
        public System.Collections.Generic.List<string> CustomProjectHistory { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> ScopeOfWorkHistory { get; set; } = new System.Collections.Generic.List<string>();
        public bool ActionCenterTrackPassportAlerts { get; set; } = true;
        public bool ActionCenterTrackBankingAlerts { get; set; } = true;
    }

    public class LocalSettingsService
    {
        private readonly ILogger<LocalSettingsService> _logger;
        private readonly IToastService _toastService;
        private readonly string _filePath;
        private LocalSettings _settings;

        public LocalSettings Settings => _settings;

        public LocalSettingsService(ILogger<LocalSettingsService> logger, IToastService toastService)
        {
            _logger = logger;
            _toastService = toastService;

            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC.WpfClient");
            _filePath = Path.Combine(folder, "settings.json");

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create local settings folder at {Folder}.", folder);
                _toastService.ShowWarning("Settings warning", "Local settings may not be saved on this device.");
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
                    var settings = JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
                    if (settings.CustomProjectHistory == null)
                    {
                        settings.CustomProjectHistory = new System.Collections.Generic.List<string>();
                    }
                    if (settings.ScopeOfWorkHistory == null)
                    {
                        settings.ScopeOfWorkHistory = new System.Collections.Generic.List<string>();
                    }
                    return settings;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load local settings from {FilePath}. Using defaults.", _filePath);
                _toastService.ShowWarning("Settings reset", "Local settings could not be loaded, so defaults were used.");
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save local settings to {FilePath}.", _filePath);
                _toastService.ShowWarning("Settings not saved", "Your local settings could not be saved.");
            }
        }

        public void AddCustomProjectHistory(string project)
        {
            if (string.IsNullOrWhiteSpace(project)) return;
            var trimmed = project.Trim();
            if (_settings.CustomProjectHistory == null)
            {
                _settings.CustomProjectHistory = new System.Collections.Generic.List<string>();
            }

            // Remove existing case-insensitive duplicate to re-insert at top
            _settings.CustomProjectHistory.RemoveAll(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
            _settings.CustomProjectHistory.Insert(0, trimmed);

            // Limit history to 50 items
            if (_settings.CustomProjectHistory.Count > 50)
            {
                _settings.CustomProjectHistory = _settings.CustomProjectHistory.GetRange(0, 50);
            }

            Save();
        }

        public void RemoveCustomProjectHistory(string project)
        {
            if (string.IsNullOrWhiteSpace(project) || _settings.CustomProjectHistory == null) return;
            var trimmed = project.Trim();
            int removed = _settings.CustomProjectHistory.RemoveAll(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save();
            }
        }

        public void AddScopeOfWorkHistory(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return;
            var trimmed = scope.Trim();
            if (_settings.ScopeOfWorkHistory == null)
            {
                _settings.ScopeOfWorkHistory = new System.Collections.Generic.List<string>();
            }

            // Remove existing case-insensitive duplicate to re-insert at top
            _settings.ScopeOfWorkHistory.RemoveAll(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            _settings.ScopeOfWorkHistory.Insert(0, trimmed);

            // Limit history to 50 items
            if (_settings.ScopeOfWorkHistory.Count > 50)
            {
                _settings.ScopeOfWorkHistory = _settings.ScopeOfWorkHistory.GetRange(0, 50);
            }

            Save();
        }

        public void RemoveScopeOfWorkHistory(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope) || _settings.ScopeOfWorkHistory == null) return;
            var trimmed = scope.Trim();
            int removed = _settings.ScopeOfWorkHistory.RemoveAll(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save();
            }
        }
    }
}
