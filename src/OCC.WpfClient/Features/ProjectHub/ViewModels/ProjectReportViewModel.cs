using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectReportViewModel : DetailViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IHealthSafetyService _healthSafetyService;
        private readonly ISubContractorService _subContractorService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ConnectionSettings _connectionSettings;

        [ObservableProperty] private Project? _project;
        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private int _totalTasks;
        [ObservableProperty] private int _inProgressTasks;
        [ObservableProperty] private int _completedTasks;
        [ObservableProperty] private double _overallProgress;
        [ObservableProperty] private double _safeWorkingHours;
        [ObservableProperty] private int _weekNumber;

        // Custom local fields
        [ObservableProperty] private string _statusSummary = string.Empty;
        [ObservableProperty] private string _generalWasteTon = "0";
        [ObservableProperty] private string _rubbleM3 = "0";
        [ObservableProperty] private string _scrapMetalsTon = "0";
        [ObservableProperty] private string _asbestosTon = "0";
        [ObservableProperty] private DateTime? _siteEstablishmentPlanned;
        [ObservableProperty] private DateTime? _siteEstablishmentActual;
        [ObservableProperty] private DateTime? _practicalCompletionPlanned;
        [ObservableProperty] private DateTime? _practicalCompletionActual;
        [ObservableProperty] private double _powPercentRequired;
        [ObservableProperty] private int _delayDays;
        [ObservableProperty] private DateTime? _streamingPlanned;
        [ObservableProperty] private DateTime? _streamingActual;

        // Display lists
        [ObservableProperty] private ObservableCollection<VendorReportRow> _vendorReportRows = new();
        [ObservableProperty] private ObservableCollection<IncidentPhotoDto> _incidentPhotos = new();
        [ObservableProperty] private ObservableCollection<ProjectVariationOrder> _variationOrders = new();
        [ObservableProperty] private bool _hasPhotos;

        private List<ProjectTask> _loadedTasks = new();

        public ProjectReportViewModel(
            IProjectService projectService,
            IHealthSafetyService healthSafetyService,
            ISubContractorService subContractorService,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ConnectionSettings connectionSettings,
            IDialogService dialogService,
            ILogger<ProjectReportViewModel> logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _projectService = projectService;
            _healthSafetyService = healthSafetyService;
            _subContractorService = subContractorService;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _connectionSettings = connectionSettings;
            Title = "Project Report";
        }

        public async Task LoadReportDataAsync(Guid projectId)
        {
            ProjectId = projectId;
            try
            {
                IsBusy = true;
                BusyText = "Loading report data...";

                Project = await _projectService.GetProjectAsync(projectId);
                if (Project == null) return;

                // Load tasks
                var tasks = (await _projectService.GetProjectTasksAsync(projectId)).ToList();
                var nonGroupTasks = tasks.Where(t => !t.IsGroup).ToList();
                _loadedTasks = nonGroupTasks;
                TotalTasks = nonGroupTasks.Count;
                InProgressTasks = nonGroupTasks.Count(t => t.Status == "In Progress" || t.Status == "Started" || (t.PercentComplete > 0 && t.PercentComplete < 100));
                CompletedTasks = nonGroupTasks.Count(t => t.Status == "Completed" || t.Status == "Done" || t.PercentComplete == 100);
                OverallProgress = TotalTasks > 0 ? (double)nonGroupTasks.Sum(t => t.PercentComplete) / TotalTasks : 0;

                // Calculate week number
                if (Project.StartDate != default)
                {
                    var days = (DateTime.Today - Project.StartDate).Days;
                    WeekNumber = Math.Max(1, (days / 7) + 1);
                }
                else
                {
                    WeekNumber = 1;
                }

                // Load local report properties
                LoadLocalReportData();

                // Fetch safe working hours
                await FetchSafeWorkingHoursAsync();

                // Fetch subcontractor audits and build vendor report
                await BuildVendorReportAsync(nonGroupTasks);

                // Fetch project incident photos
                await FetchIncidentPhotosAsync();

                // Fetch variation orders
                await FetchVariationOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project report data for project {ProjectId}", projectId);
                NotifyError("Error", "Could not load report data: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task FetchSafeWorkingHoursAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var token = _authService.CurrentToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                var url = $"{baseUrl}api/AttendanceRecords";

                var records = await client.GetFromJsonAsync<List<AttendanceRecord>>(url);
                if (records != null)
                {
                    SafeWorkingHours = records
                        .Where(r => r.ProjectId == ProjectId && r.Status == AttendanceStatus.Present)
                        .Sum(r => r.HoursWorked);
                }
                else
                {
                    SafeWorkingHours = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch attendance records for safe working hours calculation. Defaulting to 0.");
                SafeWorkingHours = 0;
            }
        }

        private async Task BuildVendorReportAsync(List<ProjectTask> nonGroupTasks)
        {
            try
            {
                VendorReportRows.Clear();

                // 1. Load project audits to match scores
                var allAudits = await _healthSafetyService.GetAuditsAsync();
                var projectAudits = allAudits
                    .Where(a => a.SiteName != null && Project != null && a.SiteName.Contains(Project.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.Date)
                    .ToList();

                string audit1 = projectAudits.Count > 0 ? $"{projectAudits[0].ActualScore}%" : "-";
                string audit2 = projectAudits.Count > 1 ? $"{projectAudits[1].ActualScore}%" : "-";
                string audit3 = projectAudits.Count > 2 ? $"{projectAudits[2].ActualScore}%" : "-";

                // 2. Add OCC (internal) row
                VendorReportRows.Add(new VendorReportRow
                {
                    VendorName = "Orange Circle Construction",
                    Scope = "Primary Contractor",
                    SafetyApproved = "Yes",
                    AppScore = "100%",
                    Audit1 = audit1,
                    Audit2 = audit2,
                    Audit3 = audit3
                });

                // 3. Extract subcontractors from task assignments
                var subbies = nonGroupTasks
                    .SelectMany(t => t.Assignments ?? new List<TaskAssignment>())
                    .Where(a => a.AssigneeType == AssigneeType.Contractor)
                    .Select(a => a.AssigneeName)
                    .Distinct()
                    .ToList();

                foreach (var name in subbies)
                {
                    // Find scope (comma separated specialties)
                    string scope = "Sub-Contractor";
                    try
                    {
                        var contractors = await _subContractorService.GetSubContractorsAsync();
                        var conObj = contractors.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (conObj != null && !string.IsNullOrEmpty(conObj.Specialties))
                        {
                            scope = conObj.Specialties;
                        }
                    }
                    catch { }

                    VendorReportRows.Add(new VendorReportRow
                    {
                        VendorName = name,
                        Scope = scope,
                        SafetyApproved = "Yes",
                        AppScore = "100%",
                        Audit1 = audit1,
                        Audit2 = audit2,
                        Audit3 = audit3
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build vendor report details.");
            }
        }

        private async Task FetchIncidentPhotosAsync()
        {
            try
            {
                IncidentPhotos.Clear();
                var incidents = await _healthSafetyService.GetIncidentsAsync();
                var projectIncidents = incidents.Where(i => i.Location != null && Project != null && i.Location.Contains(Project.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var inc in projectIncidents)
                {
                    var detail = await _healthSafetyService.GetIncidentAsync(inc.Id);
                    if (detail?.Photos != null)
                    {
                        foreach (var photo in detail.Photos)
                        {
                            IncidentPhotos.Add(photo);
                        }
                    }
                }
                HasPhotos = IncidentPhotos.Any();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch project incident photos.");
                HasPhotos = false;
            }
        }

        private async Task FetchVariationOrdersAsync()
        {
            try
            {
                VariationOrders.Clear();
                var client = _httpClientFactory.CreateClient();
                var token = _authService.CurrentToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                var url = $"{baseUrl}api/ProjectVariationOrders?projectId={ProjectId}";

                var vOs = await client.GetFromJsonAsync<List<ProjectVariationOrder>>(url);
                if (vOs != null)
                {
                    foreach (var vo in vOs)
                    {
                        VariationOrders.Add(vo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch variation orders. Defaulting to empty.");
            }
        }

        private string GetLocalFilePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OCC.WpfClient", "project_reports");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, $"{ProjectId}.json");
        }

        private void LoadLocalReportData()
        {
            try
            {
                var path = GetLocalFilePath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<LocalProjectReportData>(json);
                    if (data != null)
                    {
                        StatusSummary = data.StatusSummary;
                        GeneralWasteTon = data.GeneralWasteTon;
                        RubbleM3 = data.RubbleM3;
                        ScrapMetalsTon = data.ScrapMetalsTon;
                        AsbestosTon = data.AsbestosTon;
                        SiteEstablishmentPlanned = data.SiteEstablishmentPlanned;
                        SiteEstablishmentActual = data.SiteEstablishmentActual;
                        PracticalCompletionPlanned = data.PracticalCompletionPlanned;
                        PracticalCompletionActual = data.PracticalCompletionActual;
                        PowPercentRequired = data.PowPercentRequired;
                        DelayDays = data.DelayDays;
                        StreamingPlanned = data.StreamingPlanned;
                        StreamingActual = data.StreamingActual;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load local report properties.");
            }

            // Defaults
            StatusSummary = Project?.Description ?? string.Empty;
            GeneralWasteTon = "0";
            RubbleM3 = "0";
            ScrapMetalsTon = "0";
            AsbestosTon = "0";
            SiteEstablishmentPlanned = Project?.StartDate;
            SiteEstablishmentActual = Project?.StartDate;
            PracticalCompletionPlanned = Project?.EndDate;
            PracticalCompletionActual = Project?.EndDate;
            PowPercentRequired = 0;
            DelayDays = 0;
            StreamingPlanned = Project?.EndDate;
            StreamingActual = Project?.EndDate;
        }

        private void SaveLocalReportData()
        {
            try
            {
                var path = GetLocalFilePath();
                var data = new LocalProjectReportData
                {
                    StatusSummary = StatusSummary,
                    GeneralWasteTon = GeneralWasteTon,
                    RubbleM3 = RubbleM3,
                    ScrapMetalsTon = ScrapMetalsTon,
                    AsbestosTon = AsbestosTon,
                    SiteEstablishmentPlanned = SiteEstablishmentPlanned,
                    SiteEstablishmentActual = SiteEstablishmentActual,
                    PracticalCompletionPlanned = PracticalCompletionPlanned,
                    PracticalCompletionActual = PracticalCompletionActual,
                    PowPercentRequired = PowPercentRequired,
                    DelayDays = DelayDays,
                    StreamingPlanned = StreamingPlanned,
                    StreamingActual = StreamingActual
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save local report properties.");
            }
        }

        protected override string GetReportTitle() => $"OCC Project Report: {Project?.Name}";

        protected override object GetReportItem() => new
        {
            ProjectName = Project?.Name,
            CustomerName = Project?.Customer,
            Status = Project?.Status,
            Week = WeekNumber,
            Progress = $"{OverallProgress:F1}%",
            Tasks = TotalTasks,
            InProgressTasks = InProgressTasks,
            CompletedTasks = CompletedTasks,
            SafeWorkingHours = SafeWorkingHours,
            StatusSummary = StatusSummary,
            WasteGeneral = $"{GeneralWasteTon} TON",
            WasteRubble = $"{RubbleM3} m3",
            WasteScrap = $"{ScrapMetalsTon} TON",
            WasteAsbestos = $"{AsbestosTon} TON",
            SiteEstPlanned = SiteEstablishmentPlanned?.ToString("yyyy/MM/dd") ?? "-",
            SiteEstActual = SiteEstablishmentActual?.ToString("yyyy/MM/dd") ?? "-",
            PracCompPlanned = PracticalCompletionPlanned?.ToString("yyyy/MM/dd") ?? "-",
            PracCompActual = PracticalCompletionActual?.ToString("yyyy/MM/dd") ?? "-"
        };

        protected override async Task ExecuteSaveAsync()
        {
            SaveLocalReportData();
            if (Project != null && StatusSummary != Project.Description)
            {
                Project.Description = StatusSummary;
                await _projectService.UpdateProjectAsync(Project);
            }
            NotifySuccess("Report Saved", "Project report fields have been saved successfully.");
        }

        protected override async Task ExecuteReloadAsync()
        {
            await LoadReportDataAsync(ProjectId);
        }

        public override async Task PrintAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating Project Report PDF...";

                if (_pdfService == null)
                {
                    _logger.LogError("IPdfService is not initialized.");
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                // Download incident photos to temporary local files
                var localPhotoPaths = new List<string>();
                if (IncidentPhotos != null && IncidentPhotos.Any())
                {
                    var tempPhotosDir = Path.Combine(Path.GetTempPath(), "OCC_Report_Photos");
                    try
                    {
                        if (Directory.Exists(tempPhotosDir))
                        {
                            Directory.Delete(tempPhotosDir, true);
                        }
                    }
                    catch { }
                    
                    try
                    {
                        Directory.CreateDirectory(tempPhotosDir);
                        using var client = _httpClientFactory.CreateClient();
                        var token = _authService.CurrentToken;
                        if (!string.IsNullOrEmpty(token))
                        {
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }

                        foreach (var photo in IncidentPhotos)
                        {
                            if (string.IsNullOrEmpty(photo.FilePath)) continue;
                            try
                            {
                                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                                var fullUrl = photo.FilePath.StartsWith("http") ? photo.FilePath : $"{baseUrl}/{photo.FilePath.TrimStart('/')}";
                                var bytes = await client.GetByteArrayAsync(fullUrl);
                                var ext = Path.GetExtension(photo.FileName);
                                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                                var localFile = Path.Combine(tempPhotosDir, $"{photo.Id}{ext}");
                                await File.WriteAllBytesAsync(localFile, bytes);
                                localPhotoPaths.Add(localFile);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download incident photo {PhotoId}", photo.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize temp directory for photos.");
                    }
                }

                // Map UI fields to print model
                var model = new ProjectReportPrintModel
                {
                    Project = Project ?? new(),
                    WeekNumber = WeekNumber,
                    TotalTasks = TotalTasks,
                    InProgressTasks = InProgressTasks,
                    CompletedTasks = CompletedTasks,
                    OverallProgress = OverallProgress,
                    PowPercentRequired = PowPercentRequired,
                    DelayDays = DelayDays,
                    SafeWorkingHours = SafeWorkingHours,
                    SiteEstablishmentPlanned = SiteEstablishmentPlanned,
                    SiteEstablishmentActual = SiteEstablishmentActual,
                    PracticalCompletionPlanned = PracticalCompletionPlanned,
                    PracticalCompletionActual = PracticalCompletionActual,
                    StreamingPlanned = StreamingPlanned,
                    StreamingActual = StreamingActual,
                    GeneralWasteTon = GeneralWasteTon,
                    RubbleM3 = RubbleM3,
                    ScrapMetalsTon = ScrapMetalsTon,
                    AsbestosTon = AsbestosTon,
                    StatusSummary = StatusSummary,
                    VendorReportRows = VendorReportRows.Select(r => new ProjectReportPrintVendorRow
                    {
                        VendorName = r.VendorName,
                        Scope = r.Scope,
                        SafetyApproved = r.SafetyApproved,
                        AppScore = r.AppScore,
                        Audit1 = r.Audit1,
                        Audit2 = r.Audit2,
                        Audit3 = r.Audit3
                    }).ToList(),
                    VariationOrders = VariationOrders.ToList(),
                    IncidentPhotoPaths = localPhotoPaths
                };

                var path = await _pdfService.GenerateProjectReportPdfAsync(model);
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing project report");
                NotifyError("Print Error", $"Failed to generate project report PDF: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AutoGenerateSummary()
        {
            if (Project == null) return;
            
            var completedList = _loadedTasks?.Where(t => t.Status == "Completed" || t.Status == "Done" || t.PercentComplete == 100).Select(t => t.Name).Take(3).ToList() ?? new();
            var inProgressList = _loadedTasks?.Where(t => t.Status == "In Progress" || t.Status == "Started" || (t.PercentComplete > 0 && t.PercentComplete < 100)).Select(t => t.Name).Take(3).ToList() ?? new();
            var upcomingList = _loadedTasks?.Where(t => t.Status != "Completed" && t.Status != "Done" && t.PercentComplete == 0).Select(t => t.Name).Take(3).ToList() ?? new();

            var summary = $"As of Week {WeekNumber}, the {Project.Name} project has reached {OverallProgress * 100:F1}% overall progress (against a program requirement of {PowPercentRequired:F1}%). ";
            
            if (completedList.Any())
            {
                summary += $"Key completed works include: {string.Join(", ", completedList)}. ";
            }
            if (inProgressList.Any())
            {
                summary += $"Active works currently in progress include: {string.Join(", ", inProgressList)}. ";
            }
            else if (upcomingList.Any())
            {
                summary += $"Upcoming scheduled activities include: {string.Join(", ", upcomingList)}. ";
            }

            if (DelayDays > 0)
            {
                summary += $"The project is currently delayed by {DelayDays} days. ";
            }
            else
            {
                summary += "Works are currently progressing on schedule. ";
            }

            summary += $"A total of {SafeWorkingHours:N0} safe working hours have been recorded on site to date.";

            StatusSummary = summary;
        }
    }

    public class VendorReportRow
    {
        public string VendorName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string SafetyApproved { get; set; } = "Yes";
        public string AppScore { get; set; } = "100%";
        public string Audit1 { get; set; } = string.Empty;
        public string Audit2 { get; set; } = string.Empty;
        public string Audit3 { get; set; } = string.Empty;
    }

    public class LocalProjectReportData
    {
        public string StatusSummary { get; set; } = string.Empty;
        public string GeneralWasteTon { get; set; } = "0";
        public string RubbleM3 { get; set; } = "0";
        public string ScrapMetalsTon { get; set; } = "0";
        public string AsbestosTon { get; set; } = "0";
        public DateTime? SiteEstablishmentPlanned { get; set; }
        public DateTime? SiteEstablishmentActual { get; set; }
        public DateTime? PracticalCompletionPlanned { get; set; }
        public DateTime? PracticalCompletionActual { get; set; }
        public double PowPercentRequired { get; set; }
        public int DelayDays { get; set; }
        public DateTime? StreamingPlanned { get; set; }
        public DateTime? StreamingActual { get; set; }
    }
}
