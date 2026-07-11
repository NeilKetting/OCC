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
        public System.Collections.Generic.List<Guid>? CalendarSelectedProjectIds { get; set; }
        public System.Collections.Generic.List<string>? QuickActions { get; set; }
        public System.Collections.Generic.Dictionary<string, bool>? WageRunVisibleColumns { get; set; }
        public System.Collections.Generic.List<OCC.WpfClient.Features.Main.Models.WidgetConfig>? DashboardWidgets { get; set; }
        public bool DisableOutlookSync { get; set; } = false;
        public bool MuteOutlookReminders { get; set; } = false;
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
                    return JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
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
    }
}
