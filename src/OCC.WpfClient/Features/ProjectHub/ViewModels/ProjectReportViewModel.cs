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
using OCC.WpfClient.Features.ProjectHub.Models;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;

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
        private readonly IProjectReportService _projectReportService;

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
        [ObservableProperty] private double _powPercentRequired;
        [ObservableProperty] private int _delayDays;

        // Dynamic Milestones
        [ObservableProperty] private ObservableCollection<MilestoneReportItem> _thisWeekMilestones = new();
        [ObservableProperty] private ObservableCollection<MilestoneReportItem> _overdueMilestones = new();
        [ObservableProperty] private bool _hasThisWeekMilestones;
        [ObservableProperty] private bool _noThisWeekMilestones = true;
        [ObservableProperty] private bool _hasOverdueMilestones;
        [ObservableProperty] private bool _noOverdueMilestones = true;

        // Display lists
        [ObservableProperty] private ObservableCollection<VendorReportRow> _vendorReportRows = new();
        [ObservableProperty] private ObservableCollection<VendorReportRow> _primaryContractorRows = new();
        [ObservableProperty] private ObservableCollection<VendorReportRow> _subContractorRows = new();
        [ObservableProperty] private ObservableCollection<string> _reportPhotos = new();
        [ObservableProperty] private ObservableCollection<ProjectVariationOrder> _variationOrders = new();
        [ObservableProperty] private ObservableCollection<ProjectReportHistory> _reportHistory = new();
        [ObservableProperty] private bool _hasPhotos;

        private List<ProjectTask> _loadedTasks = new();
        private ProjectReportDraft? _currentDraft;

        public ProjectReportViewModel(
            IProjectService projectService,
            IHealthSafetyService healthSafetyService,
            ISubContractorService subContractorService,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ConnectionSettings connectionSettings,
            IDialogService dialogService,
            ILogger<ProjectReportViewModel> logger,
            IPdfService pdfService,
            IProjectReportService projectReportService) : base(dialogService, logger, pdfService)
        {
            _projectService = projectService;
            _healthSafetyService = healthSafetyService;
            _subContractorService = subContractorService;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _connectionSettings = connectionSettings;
            _projectReportService = projectReportService;
            Title = "Project Report";
        }

        public async Task LoadReportDataAsync(Guid projectId, bool autoGenerate = false, bool silent = false)
        {
            ProjectId = projectId;
            try
            {
                if (!silent)
                {
                    IsBusy = true;
                    BusyText = "Loading report data...";
                }

                Project = await _projectService.GetProjectAsync(projectId);
                if (Project == null) return;

                // Load tasks
                var tasks = (await _projectService.GetProjectTasksAsync(projectId)).ToList();
                var nonGroupTasks = tasks.Where(t => !t.IsGroup).ToList();
                _loadedTasks = nonGroupTasks;
                TotalTasks = nonGroupTasks.Count;
                InProgressTasks = nonGroupTasks.Count(t => !t.IsComplete && (t.PercentComplete > 0 || t.Status == "In Progress" || t.Status == "Started" || t.Status == "Halfway" || t.Status == "Almost Done" || (t.Status != "Not Started" && t.Status != "To Do" && t.Status != "New" && t.Status != "On Hold" && t.Status != "Cancelled")));
                CompletedTasks = nonGroupTasks.Count(t => t.Status == "Completed" || t.Status == "Done" || t.PercentComplete == 100);
                OverallProgress = TotalTasks > 0 ? (double)nonGroupTasks.Sum(t => t.PercentComplete) / TotalTasks : 0;

                // Calculate week number based on Thursday-to-Thursday reporting cycle
                if (Project.StartDate != default)
                {
                    // Find the first Thursday on or after Project.StartDate
                    var firstThursday = Project.StartDate.Date;
                    while (firstThursday.DayOfWeek != DayOfWeek.Thursday)
                    {
                        firstThursday = firstThursday.AddDays(1);
                    }

                    if (DateTime.Today <= firstThursday)
                    {
                        WeekNumber = 1;
                    }
                    else
                    {
                        var daysSinceFirstThursday = (DateTime.Today - firstThursday).Days;
                        WeekNumber = ((daysSinceFirstThursday - 1) / 7) + 2;
                    }
                }
                else
                {
                    WeekNumber = 1;
                }

                // Load report draft details from API
                var draft = await _projectReportService.GetDraftAsync(projectId);
                _currentDraft = draft;
                if (draft != null)
                {
                    StatusSummary = draft.StatusSummary;
                    GeneralWasteTon = draft.GeneralWasteTon;
                    RubbleM3 = draft.RubbleM3;
                    ScrapMetalsTon = draft.ScrapMetalsTon;
                    AsbestosTon = draft.AsbestosTon;
                    PowPercentRequired = draft.PowPercentRequired;
                    LoadPhotosFromDraft(draft.PhotoUrls);
                }
                else
                {
                    _currentDraft = null;
                    // Defaults
                    StatusSummary = Project?.Description ?? string.Empty;
                    GeneralWasteTon = "0";
                    RubbleM3 = "0";
                    ScrapMetalsTon = "0";
                    AsbestosTon = "0";
                    PowPercentRequired = 0;
                    DelayDays = 0;
                    ReportPhotos.Clear();
                    HasPhotos = false;
                }

                // Calculate Comprehensive POW progress
                var today = DateTime.Today;
                double totalLeafPlannedProgress = 0;
                foreach (var task in nonGroupTasks)
                {
                    double plannedProgress = 0;
                    if (today < task.StartDate)
                    {
                        plannedProgress = 0;
                    }
                    else if (today > task.FinishDate)
                    {
                        plannedProgress = 100;
                    }
                    else
                    {
                        double totalDuration = (task.FinishDate - task.StartDate).TotalSeconds;
                        if (totalDuration <= 0)
                        {
                            plannedProgress = 100;
                        }
                        else
                        {
                            double elapsed = (today - task.StartDate).TotalSeconds;
                            plannedProgress = (elapsed / totalDuration) * 100;
                        }
                    }
                    totalLeafPlannedProgress += plannedProgress;
                }
                PowPercentRequired = nonGroupTasks.Count > 0 ? totalLeafPlannedProgress / nonGroupTasks.Count : 0;

                // Load reasons map
                var reasonsMap = new Dictionary<Guid, string>();
                if (draft != null && !string.IsNullOrEmpty(draft.OverdueMilestoneReasons))
                {
                    try
                    {
                        reasonsMap = JsonSerializer.Deserialize<Dictionary<Guid, string>>(draft.OverdueMilestoneReasons) ?? new Dictionary<Guid, string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize overdue milestone reasons.");
                    }
                }

                // Calculate reporting week start/end
                var weekEndDate = today;
                while (weekEndDate.DayOfWeek != DayOfWeek.Thursday)
                {
                    weekEndDate = weekEndDate.AddDays(1);
                }
                var weekStartDate = weekEndDate.AddDays(-6);

                // Load dynamic milestones
                ProjectTask? projectTask = null;
                if (Project != null && !string.IsNullOrEmpty(Project.Name))
                {
                    projectTask = tasks.FirstOrDefault(t => t.Name.Trim().Equals(Project.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (projectTask == null)
                    {
                        projectTask = tasks.FirstOrDefault(t => t.Name.Contains(Project.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (projectTask != null)
                    {
                        var lookup = tasks.ToDictionary(t => t.Id);
                        bool isRoot = !projectTask.ParentId.HasValue || projectTask.ParentId == Guid.Empty || !lookup.ContainsKey(projectTask.ParentId.Value);
                        if (isRoot)
                        {
                            var childGroups = tasks.Where(t => t.ParentId == projectTask.Id && t.IsGroup).ToList();
                            if (childGroups.Count == 1)
                            {
                                projectTask = childGroups[0];
                            }
                        }
                    }
                }

                List<ProjectTask> parentTasks;
                if (projectTask != null)
                {
                    parentTasks = tasks.Where(t => t.IsGroup && t.ParentId == projectTask.Id).ToList();
                }
                else
                {
                    var lookup = tasks.ToDictionary(t => t.Id);
                    var roots = tasks.Where(t => !t.ParentId.HasValue || t.ParentId == Guid.Empty || !lookup.ContainsKey(t.ParentId.Value)).ToList();
                    if (roots.Count == 1)
                    {
                        var root = roots[0];
                        bool nameMatchesProject = false;
                        if (Project != null && !string.IsNullOrEmpty(Project.Name))
                        {
                            nameMatchesProject = root.Name.Contains(Project.Name, StringComparison.OrdinalIgnoreCase) ||
                                                 Project.Name.Contains(root.Name, StringComparison.OrdinalIgnoreCase);
                        }

                        if (!nameMatchesProject && root.IsGroup)
                        {
                            parentTasks = new List<ProjectTask> { root };
                        }
                        else
                        {
                            var rootChildren = tasks.Where(t => t.ParentId == root.Id).ToList();
                            var childGroups = rootChildren.Where(t => t.IsGroup).ToList();
                            if (childGroups.Count == 1)
                            {
                                var projectTaskCandidate = childGroups[0];
                                parentTasks = tasks.Where(t => t.IsGroup && t.ParentId == projectTaskCandidate.Id).ToList();
                            }
                            else
                            {
                                parentTasks = tasks.Where(t => t.IsGroup && t.ParentId == root.Id).ToList();
                            }
                        }
                    }
                    else
                    {
                        parentTasks = tasks.Where(t => t.IsGroup && (!t.ParentId.HasValue || t.ParentId == Guid.Empty || !lookup.ContainsKey(t.ParentId.Value))).ToList();
                    }
                }

                var thisWeekList = new List<MilestoneReportItem>();
                var overdueList = new List<MilestoneReportItem>();

                var workingDays = GetUpcomingWorkingDays(today, 5);
                var minWorkingDay = workingDays[0];
                var maxWorkingDay = workingDays[4];

                foreach (var pt in parentTasks)
                {
                    if (pt.FinishDate.Date < today && !pt.IsComplete)
                    {
                        var item = new MilestoneReportItem
                        {
                            TaskId = pt.Id,
                            Name = pt.Name,
                            StartDate = pt.StartDate,
                            PlannedDate = pt.FinishDate,
                            Progress = pt.PercentComplete,
                            Status = pt.Status,
                            IsComplete = pt.IsComplete
                        };
                        if (reasonsMap.TryGetValue(pt.Id, out var reason))
                        {
                            item.Reason = reason;
                        }
                        overdueList.Add(item);
                    }
                    else
                    {
                        bool isThisWeek = false;
                        if (pt.IsComplete)
                        {
                            isThisWeek = pt.FinishDate.Date >= minWorkingDay && pt.FinishDate.Date <= maxWorkingDay;
                        }
                        else
                        {
                            isThisWeek = pt.StartDate.Date <= maxWorkingDay && pt.FinishDate.Date >= minWorkingDay;
                        }

                        if (isThisWeek)
                        {
                            var item = new MilestoneReportItem
                            {
                                TaskId = pt.Id,
                                Name = pt.Name,
                                StartDate = pt.StartDate,
                                PlannedDate = pt.FinishDate,
                                Progress = pt.PercentComplete,
                                Status = pt.Status,
                                IsComplete = pt.IsComplete
                            };
                            if (reasonsMap.TryGetValue(pt.Id, out var reason))
                            {
                                item.Reason = reason;
                            }
                            thisWeekList.Add(item);
                        }
                    }
                }

                ThisWeekMilestones = new ObservableCollection<MilestoneReportItem>(thisWeekList);
                OverdueMilestones = new ObservableCollection<MilestoneReportItem>(overdueList);

                HasThisWeekMilestones = ThisWeekMilestones.Any();
                NoThisWeekMilestones = !HasThisWeekMilestones;
                HasOverdueMilestones = OverdueMilestones.Any();
                NoOverdueMilestones = !HasOverdueMilestones;

                // Load report run history
                await LoadHistoryAsync();

                // Fetch safe working hours
                await FetchSafeWorkingHoursAsync();

                // Fetch subcontractor audits and build vendor report
                await BuildVendorReportAsync(nonGroupTasks);

                // Fetch variation orders
                await FetchVariationOrdersAsync();

                if (autoGenerate)
                {
                    AutoGenerateSummary();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION IN LoadReportDataAsync: {ex}");
                _logger.LogError(ex, "Error loading project report data for project {ProjectId}", projectId);
                if (!silent)
                {
                    NotifyError("Error", "Could not load report data: " + ex.Message);
                }
            }
            finally
            {
                if (!silent)
                {
                    IsBusy = false;
                }
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
                var url = $"{baseUrl}api/HseqStats/project/{ProjectId}";

                SafeWorkingHours = await client.GetFromJsonAsync<double>(url);
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
                PrimaryContractorRows.Clear();
                SubContractorRows.Clear();

                // 1. Load project audits to match scores
                var allAudits = await _healthSafetyService.GetAuditsAsync(ProjectId);
                var projectAudits = allAudits
                    .OrderBy(a => a.Date)
                    .ToList();

                string audit1 = projectAudits.Count > 0 ? $"{projectAudits[0].ActualScore}%" : "-";
                string audit2 = projectAudits.Count > 1 ? $"{projectAudits[1].ActualScore}%" : "-";
                string audit3 = projectAudits.Count > 2 ? $"{projectAudits[2].ActualScore}%" : "-";

                // 2. Add OCC (internal) row
                var occRow = new VendorReportRow
                {
                    VendorName = "Orange Circle Construction",
                    Scope = "Primary Contractor",
                    SafetyApproved = "Yes",
                    AppScore = "100%",
                    Audit1 = audit1,
                    Audit2 = audit2,
                    Audit3 = audit3
                };
                ApplyManualOverrideIfPresent(occRow);
                VendorReportRows.Add(occRow);
                PrimaryContractorRows.Add(occRow);

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
                    string appScore = "100%";
                    try
                    {
                        var contractors = await _subContractorService.GetSubContractorsAsync();
                        var conObj = contractors.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (conObj != null)
                        {
                            if (!string.IsNullOrEmpty(conObj.Specialties))
                            {
                                scope = conObj.Specialties;
                            }
                            appScore = $"{conObj.OnTimeRate:0}%";
                        }
                    }
                    catch { }

                    var subRow = new VendorReportRow
                    {
                        VendorName = name,
                        Scope = scope,
                        SafetyApproved = "Pending",
                        AppScore = appScore,
                        Audit1 = "-",
                        Audit2 = "-",
                        Audit3 = "-"
                    };
                    ApplyManualOverrideIfPresent(subRow);
                    VendorReportRows.Add(subRow);
                    SubContractorRows.Add(subRow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building vendor report");
            }
        }

        private void ApplyManualOverrideIfPresent(VendorReportRow row)
        {
            if (_currentDraft != null && !string.IsNullOrEmpty(_currentDraft.ManualVendorDataJson))
            {
                try
                {
                    var manualEntries = JsonSerializer.Deserialize<List<VendorReportRow>>(_currentDraft.ManualVendorDataJson);
                    var matched = manualEntries?.FirstOrDefault(e => e.VendorName.Equals(row.VendorName, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        row.SafetyApproved = matched.SafetyApproved;
                        row.AppScore = matched.AppScore;
                        row.Audit1 = matched.Audit1;
                        row.Audit2 = matched.Audit2;
                        row.Audit3 = matched.Audit3;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize manual vendor entry overrides.");
                }
            }
        }

        // Removed FetchIncidentPhotosAsync in favor of direct draft report photo attachments

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

        private async Task LoadHistoryAsync()
        {
            try
            {
                var list = await _projectReportService.GetHistoryAsync(ProjectId);
                ReportHistory.Clear();
                foreach (var item in list)
                {
                    ReportHistory.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load report history for project {ProjectId}", ProjectId);
            }
        }

        [RelayCommand]
        public void DownloadReport(ProjectReportHistory history)
        {
            if (history == null || string.IsNullOrEmpty(history.FilePath)) return;

            try
            {
                var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                var fullUrl = history.FilePath.StartsWith("http") ? history.FilePath : $"{baseUrl}{history.FilePath.TrimStart('/')}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fullUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Could not open historical report: " + ex.Message);
                _logger.LogError(ex, "Error opening historical report {HistoryId}", history.Id);
            }
        }

        private async Task SaveLocalReportDataAsync()
        {
            try
            {
                var reasonsMap = new Dictionary<Guid, string>();
                if (ThisWeekMilestones != null)
                {
                    foreach (var m in ThisWeekMilestones)
                    {
                        if (!string.IsNullOrEmpty(m.Reason))
                        {
                            reasonsMap[m.TaskId] = m.Reason;
                        }
                    }
                }
                if (OverdueMilestones != null)
                {
                    foreach (var m in OverdueMilestones)
                    {
                        if (!string.IsNullOrEmpty(m.Reason))
                        {
                            reasonsMap[m.TaskId] = m.Reason;
                        }
                    }
                }
                var overdueMilestoneReasonsJson = JsonSerializer.Serialize(reasonsMap);

                var baseUrl = _connectionSettings.ApiBaseUrl?.TrimEnd('/') ?? "";
                var relativeUrls = new List<string>();
                foreach (var url in ReportPhotos)
                {
                    if (!string.IsNullOrEmpty(baseUrl) && url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        relativeUrls.Add(url.Substring(baseUrl.Length));
                    }
                    else
                    {
                        relativeUrls.Add(url);
                    }
                }

                var manualVendorJson = JsonSerializer.Serialize(VendorReportRows.ToList());

                var draft = new ProjectReportDraft
                {
                    ProjectId = ProjectId,
                    StatusSummary = StatusSummary,
                    GeneralWasteTon = GeneralWasteTon,
                    RubbleM3 = RubbleM3,
                    ScrapMetalsTon = ScrapMetalsTon,
                    AsbestosTon = AsbestosTon,
                    PowPercentRequired = PowPercentRequired,
                    DelayDays = DelayDays,
                    OverdueMilestoneReasons = overdueMilestoneReasonsJson,
                    PhotoUrls = string.Join(";", relativeUrls),
                    ManualVendorDataJson = manualVendorJson
                };

                await _projectReportService.SaveDraftAsync(ProjectId, draft);
                _currentDraft = draft;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save report draft to server.");
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
            WasteAsbestos = $"{AsbestosTon} TON"
        };

        protected override async Task ExecuteSaveAsync()
        {
            await SaveLocalReportDataAsync();
            WeakReferenceMessenger.Default.Send(new ProjectUpdatedMessage(ProjectId));
            NotifySuccess("Report Saved", "Project report fields have been saved successfully.");
        }

        protected override async Task ExecuteReloadAsync()
        {
            await LoadReportDataAsync(ProjectId, autoGenerate: true);
        }

        public override async Task PrintAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Saving current progress...";

                // Save current edits first so they are not lost when reloading
                await SaveLocalReportDataAsync();

                BusyText = "Recalculating milestones based on today's date...";
                // Reload/recalculate everything based on DateTime.Today
                await LoadReportDataAsync(ProjectId);

                IsBusy = true;
                BusyText = "Generating Project Report PDF...";

                if (_pdfService == null)
                {
                    _logger.LogError("IPdfService is not initialized.");
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                // Download customer logo to temporary local file if available
                string? customerLogoLocalPath = null;
                var tempReportDir = Path.Combine(Path.GetTempPath(), "OCC_Report_Temp");
                try
                {
                    if (!Directory.Exists(tempReportDir))
                    {
                        Directory.CreateDirectory(tempReportDir);
                    }
                }
                catch { }

                if (Project?.CustomerEntity != null && !string.IsNullOrEmpty(Project.CustomerEntity.LogoUrl))
                {
                    try
                    {
                        using var client = _httpClientFactory.CreateClient();
                        var token = _authService.CurrentToken;
                        if (!string.IsNullOrEmpty(token))
                        {
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }

                        var logoUrl = Project.CustomerEntity.LogoUrl;
                        var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                        var fullUrl = logoUrl.StartsWith("http") ? logoUrl : $"{baseUrl}/{logoUrl.TrimStart('/')}";
                        var bytes = await client.GetByteArrayAsync(fullUrl);
                        var ext = Path.GetExtension(logoUrl);
                        if (string.IsNullOrEmpty(ext)) ext = ".png";
                        customerLogoLocalPath = Path.Combine(tempReportDir, $"customer_logo_{Project.CustomerEntity.Id}{ext}");
                        await File.WriteAllBytesAsync(customerLogoLocalPath, bytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download customer logo {LogoUrl}", Project.CustomerEntity.LogoUrl);
                    }
                }

                // Download report photos to temporary local files in an isolated folder per project run
                var localPhotoPaths = new List<string>();
                var tempPhotosDir = Path.Combine(Path.GetTempPath(), $"OCC_Report_Photos_{ProjectId}_{Guid.NewGuid():N}");
                
                try
                {
                    if (ReportPhotos != null && ReportPhotos.Any())
                    {
                        Directory.CreateDirectory(tempPhotosDir);
                        using var client = _httpClientFactory.CreateClient();
                        var token = _authService.CurrentToken;
                        if (!string.IsNullOrEmpty(token))
                        {
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }

                        int photoIndex = 1;
                        foreach (var url in ReportPhotos)
                        {
                            if (string.IsNullOrEmpty(url)) continue;
                            try
                            {
                                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                                var fullUrl = url.StartsWith("http") ? url : $"{baseUrl}/{url.TrimStart('/')}";
                                var bytes = await client.GetByteArrayAsync(fullUrl);
                                var ext = Path.GetExtension(url);
                                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                                var localFile = Path.Combine(tempPhotosDir, $"report_photo_{photoIndex}{ext}");
                                await File.WriteAllBytesAsync(localFile, bytes);
                                localPhotoPaths.Add(localFile);
                                photoIndex++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download report photo {Url}", url);
                            }
                        }
                    }

                    // Map UI fields to print model
                    var model = new ProjectReportPrintModel
                    {
                        Project = Project ?? new(),
                        ReportDate = DateTime.Today,
                        WeekNumber = WeekNumber,
                        TotalTasks = TotalTasks,
                        InProgressTasks = InProgressTasks,
                        CompletedTasks = CompletedTasks,
                        OverallProgress = OverallProgress,
                        PowPercentRequired = PowPercentRequired,
                        DelayDays = DelayDays,
                        SafeWorkingHours = SafeWorkingHours,
                        CustomerLogoPath = customerLogoLocalPath,
                        ThisWeekMilestones = ThisWeekMilestones.Select(m => new MilestonePrintModel
                        {
                            Name = m.Name,
                            StartDate = m.StartDate,
                            PlannedDate = m.PlannedDate,
                            Progress = m.Progress,
                            Status = m.Status,
                            Reason = m.Reason,
                            IsComplete = m.IsComplete
                        }).ToList(),
                        OverdueMilestones = OverdueMilestones.Select(m => new MilestonePrintModel
                        {
                            Name = m.Name,
                            StartDate = m.StartDate,
                            PlannedDate = m.PlannedDate,
                            Progress = m.Progress,
                            Status = m.Status,
                            Reason = m.Reason,
                            IsComplete = m.IsComplete
                        }).ToList(),
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

                    // Save report PDF to Report History on server
                    try
                    {
                        using var pdfStream = System.IO.File.OpenRead(path);
                        await _projectReportService.UploadReportAsync(ProjectId, WeekNumber, $"Week {WeekNumber} Report ({DateTime.Today:yyyy-MM-dd})", pdfStream, Path.GetFileName(path));
                        await LoadHistoryAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to upload generated PDF to report history.");
                    }

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(tempPhotosDir))
                        {
                            Directory.Delete(tempPhotosDir, true);
                        }
                    }
                    catch { }
                }
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
            
            var completedList = _loadedTasks?
                .Where(t => t.IsComplete)
                .OrderByDescending(t => t.ActualCompleteDate ?? t.FinishDate)
                .Select(t => t.Name)
                .Take(3)
                .ToList() ?? new();

            var inProgressList = _loadedTasks?
                .Where(t => !t.IsComplete && (t.Status == "In Progress" || t.Status == "Started" || (t.PercentComplete > 0 && t.PercentComplete < 100)))
                .OrderByDescending(t => t.PercentComplete)
                .ThenByDescending(t => t.StartDate)
                .Select(t => t.Name)
                .Take(3)
                .ToList() ?? new();

            var upcomingList = _loadedTasks?
                .Where(t => !t.IsComplete && t.PercentComplete == 0 && t.Status != "In Progress" && t.Status != "Started")
                .OrderBy(t => t.StartDate)
                .Select(t => t.Name)
                .Take(3)
                .ToList() ?? new();

            var summaryParts = new List<string>();

            // 1. Completed Works
            if (completedList.Any())
            {
                var completedStr = FormatList(completedList);
                var verb = (completedList.Count > 1 || completedStr.EndsWith("s") || completedStr.Contains(" and ")) ? "are" : "is";
                summaryParts.Add($"{completedStr} {verb} now fully complete, allowing the team to transition into the next stages.");
            }
            else
            {
                summaryParts.Add("Initial project planning and mobilization are complete, transitioning into active work.");
            }

            // 2. In Progress Works
            if (inProgressList.Any())
            {
                if (inProgressList.Count == 1)
                {
                    var progressStr = inProgressList[0];
                    var verb = (progressStr.EndsWith("s") || progressStr.Contains(" and ")) ? "are" : "is";
                    summaryParts.Add($"{progressStr} {verb} currently in progress.");
                }
                else
                {
                    var firstStr = inProgressList[0];
                    var firstVerb = (firstStr.EndsWith("s") || firstStr.Contains(" and ")) ? "are" : "is";
                    var remainingList = inProgressList.Skip(1).ToList();
                    var remainingStr = FormatList(remainingList);
                    
                    var remainingPrefix = "";
                    if (!remainingStr.StartsWith("the ", StringComparison.OrdinalIgnoreCase) && 
                        !remainingStr.StartsWith("work on", StringComparison.OrdinalIgnoreCase) &&
                        !remainingStr.EndsWith("ing", StringComparison.OrdinalIgnoreCase))
                    {
                        remainingPrefix = "the ";
                    }
                    
                    summaryParts.Add($"{firstStr} {firstVerb} currently in progress, and we have officially commenced {remainingPrefix}{remainingStr}.");
                }
            }

            // 3. Upcoming Works
            if (upcomingList.Any())
            {
                var upcomingStr = FormatList(upcomingList);
                summaryParts.Add($"Looking ahead to next week, we are expecting to begin work on {upcomingStr}.");
            }

            // 4. Status
            if (DelayDays > 0)
            {
                summaryParts.Add($"Project Delayed by {DelayDays} days.");
            }
            else
            {
                summaryParts.Add("Project on Track");
            }

            StatusSummary = string.Join(" ", summaryParts);
        }

        private string FormatList(List<string> items)
        {
            if (items == null || !items.Any()) return string.Empty;
            var cleaned = items.Select(x => x.Trim()).ToList();
            if (cleaned.Count == 1) return cleaned[0];
            if (cleaned.Count == 2) return $"{cleaned[0]} and {cleaned[1]}";
            return $"{string.Join(", ", cleaned.Take(cleaned.Count - 1))}, and {cleaned.Last()}";
        }

        private List<DateTime> GetUpcomingWorkingDays(DateTime startFrom, int count)
        {
            var workingDays = new List<DateTime>();
            var current = startFrom.Date;
            while (workingDays.Count < count)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    workingDays.Add(current);
                }
                current = current.AddDays(1);
            }
            return workingDays;
        }

        private void LoadPhotosFromDraft(string photoUrlsStr)
        {
            ExecuteOnUIThread(() =>
            {
                ReportPhotos.Clear();
                if (!string.IsNullOrEmpty(photoUrlsStr))
                {
                    var baseUrl = _connectionSettings.ApiBaseUrl?.TrimEnd('/') ?? "";
                    var urls = photoUrlsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var url in urls)
                    {
                        var fullUrl = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
                            ? url 
                            : $"{baseUrl}/{url.TrimStart('/')}";
                        ReportPhotos.Add(fullUrl);
                    }
                }
                HasPhotos = ReportPhotos.Any();
            });
        }

        [RelayCommand]
        private async Task UploadPhotoAsync()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Select Project Report Photo(s)",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                var fileNames = openFileDialog.FileNames;
                int total = fileNames.Length;
                int successCount = 0;

                IsBusy = true;

                for (int i = 0; i < total; i++)
                {
                    var fileName = fileNames[i];
                    BusyText = total > 1 
                        ? $"Uploading photo {i + 1} of {total}..." 
                        : "Uploading photo...";

                    try
                    {
                        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                        var relativeUrl = await _projectReportService.UploadReportPhotoAsync(stream, Path.GetFileName(fileName));

                        if (!string.IsNullOrEmpty(relativeUrl))
                        {
                            var baseUrl = _connectionSettings.ApiBaseUrl?.TrimEnd('/') ?? "";
                            var fullUrl = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
                                ? relativeUrl 
                                : $"{baseUrl}/{relativeUrl.TrimStart('/')}";

                            ExecuteOnUIThread(() =>
                            {
                                if (!ReportPhotos.Contains(fullUrl))
                                {
                                    ReportPhotos.Add(fullUrl);
                                }
                                HasPhotos = ReportPhotos.Any();
                            });
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading photo {FileName}", fileName);
                    }
                }

                IsBusy = false;

                if (successCount > 0)
                {
                    await SaveLocalReportDataAsync();
                    if (successCount == 1)
                    {
                        NotifySuccess("Upload Success", "Photo uploaded and added to the report.");
                    }
                    else
                    {
                        NotifySuccess("Upload Success", $"{successCount} photos uploaded and added to the report.");
                    }
                }
                else
                {
                    NotifyError("Upload Error", "Failed to upload selected photos.");
                }
            }
        }

        [RelayCommand]
        private async Task RemovePhotoAsync(string photoUrl)
        {
            if (photoUrl == null) return;
            try
            {
                ExecuteOnUIThread(() =>
                {
                    ReportPhotos.Remove(photoUrl);
                    HasPhotos = ReportPhotos.Any();
                });
                await SaveLocalReportDataAsync();
                NotifySuccess("Removed", "Photo removed from the report.");
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Could not remove photo: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ClearPhotosAsync()
        {
            try
            {
                ExecuteOnUIThread(() =>
                {
                    ReportPhotos.Clear();
                    HasPhotos = false;
                });
                await SaveLocalReportDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear photos.");
            }
        }

        private void ExecuteOnUIThread(Action action)
        {
            if (App.Current?.Dispatcher == null)
            {
                action();
            }
            else
            {
                App.Current.Dispatcher.Invoke(action);
            }
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
