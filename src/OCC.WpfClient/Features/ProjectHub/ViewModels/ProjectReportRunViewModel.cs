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
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProjectHub.Models;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectReportRunViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IHealthSafetyService _healthSafetyService;
        private readonly ISubContractorService _subContractorService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ConnectionSettings _connectionSettings;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ProjectReportRunViewModel> _logger;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;
        private readonly IProjectReportService _projectReportService;

        [ObservableProperty] private ObservableCollection<ProjectReportRunItemViewModel> _runItems = new();
        [ObservableProperty] private ProjectReportRunItemViewModel? _selectedItem;
        [ObservableProperty] private string _batchStatusText = "Ready";
        [ObservableProperty] private double _batchProgress;
        [ObservableProperty] private bool _isGenerating;
        [ObservableProperty] private bool _selectAllChecked = true;

        public ProjectReportRunViewModel(
            IProjectService projectService,
            IHealthSafetyService healthSafetyService,
            ISubContractorService subContractorService,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            ConnectionSettings connectionSettings,
            IDialogService dialogService,
            ILoggerFactory loggerFactory,
            IPdfService pdfService,
            IProjectReportService projectReportService) : base()
        {
            _projectService = projectService;
            _healthSafetyService = healthSafetyService;
            _subContractorService = subContractorService;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _connectionSettings = connectionSettings;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ProjectReportRunViewModel>();
            _dialogService = dialogService;
            _pdfService = pdfService;
            _projectReportService = projectReportService;

            Title = "Project Report Run";
            _ = LoadProjectsAsync();
        }

        public async Task LoadProjectsAsync()
        {
            IsBusy = true;
            BusyText = "Loading projects for report run...";
            try
            {
                var summaries = await _projectService.GetProjectSummariesAsync(false);
                var activeSummaries = summaries.Where(p => p.Status == "Active" || p.Status == "Planning" || p.Status == "In Progress").ToList();

                RunItems.Clear();

                var loadTasks = activeSummaries.Select(async summary =>
                {
                    var item = new ProjectReportRunItemViewModel(summary, _projectService, _projectReportService, _loggerFactory.CreateLogger<ProjectReportRunItemViewModel>());
                    await item.LoadDetailsAsync();
                    return item;
                });

                var results = await Task.WhenAll(loadTasks);
                foreach (var item in results)
                {
                    item.IsSelected = true; // Default to selected
                    RunItems.Add(item);
                }

                if (RunItems.Any())
                {
                    SelectedItem = RunItems.First();
                }

                SelectAllChecked = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading projects for report run");
                NotifyError("Error", "Could not load projects: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ToggleSelectAll()
        {
            foreach (var item in RunItems)
            {
                item.IsSelected = SelectAllChecked;
            }
        }

        [RelayCommand]
        private async Task AutoGenerateSelectedSummaryAsync()
        {
            if (SelectedItem != null)
            {
                SelectedItem.AutoGenerateSummary();
                await SelectedItem.SaveLocalReportDataAsync();
            }
        }

        [RelayCommand]
        private async Task SaveSelectedItemAsync()
        {
            if (SelectedItem != null)
            {
                await SelectedItem.SaveLocalReportDataAsync();
                NotifySuccess("Saved", $"Draft values for '{SelectedItem.ProjectSummary.Name}' saved successfully.");
            }
        }

        [RelayCommand]
        private async Task GenerateReportsAsync()
        {
            var selected = RunItems.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                NotifyError("No Selection", "Please select at least one project to generate reports.");
                return;
            }

            var confirm = await _dialogService.ShowConfirmationAsync(
                "Generate Reports",
                $"Are you sure you want to generate reports for {selected.Count} selected projects?");
            if (!confirm) return;

            IsGenerating = true;
            BatchProgress = 0;

            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var runFolder = Path.Combine(documentsFolder, "OCC Report Runs", $"Run_{DateTime.Now:yyyyMMdd_HHmmss}");

            try
            {
                Directory.CreateDirectory(runFolder);

                for (int i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    item.GenerationStatus = "Generating...";
                    BatchStatusText = $"Generating report for '{item.ProjectSummary.Name}' ({i + 1} of {selected.Count})...";

                    try
                    {
                        // Save current state first
                        await item.SaveLocalReportDataAsync();

                        // Reload details to recalculate milestones, POW, etc. based on DateTime.Today!
                        await item.LoadDetailsAsync();

                        // 1. Fetch Safe Working Hours
                        var safeHours = await FetchSafeWorkingHoursAsync(item.ProjectSummary.Id);

                        // 2. Fetch Vendor Compliance Rows
                        var vendors = await BuildVendorReportAsync(item.ProjectSummary.Id, item.ProjectSummary.Name, item.LoadedTasks);

                        // 3. Fetch Variation Orders
                        var variations = await FetchVariationOrdersAsync(item.ProjectSummary.Id);

                        // 4. Fetch Incident Photos
                        var localPhotoPaths = await FetchAndDownloadIncidentPhotosAsync(item.ProjectSummary.Id, item.ProjectSummary.Name);

                        // Create Print Model
                        var model = new ProjectReportPrintModel
                        {
                            Project = item.ProjectDetails ?? new Project { Name = item.ProjectSummary.Name },
                            ReportDate = DateTime.Today,
                            WeekNumber = item.WeekNumber,
                            TotalTasks = item.LoadedTasks.Count(t => !t.IsGroup),
                            InProgressTasks = item.LoadedTasks.Count(t => !t.IsGroup && (t.Status == "In Progress" || t.Status == "Started" || (t.PercentComplete > 0 && t.PercentComplete < 100))),
                            CompletedTasks = item.LoadedTasks.Count(t => !t.IsGroup && (t.Status == "Completed" || t.Status == "Done" || t.PercentComplete == 100)),
                            OverallProgress = item.LoadedTasks.Count(t => !t.IsGroup) > 0 ? (double)item.LoadedTasks.Where(t => !t.IsGroup).Sum(t => t.PercentComplete) / item.LoadedTasks.Count(t => !t.IsGroup) : 0,
                            PowPercentRequired = item.PowPercentRequired,
                            DelayDays = item.DelayDays,
                            SafeWorkingHours = safeHours,
                            ThisWeekMilestones = item.ThisWeekMilestones.Select(m => new MilestonePrintModel
                            {
                                Name = m.Name,
                                StartDate = m.StartDate,
                                PlannedDate = m.PlannedDate,
                                Progress = m.Progress,
                                Status = m.Status,
                                Reason = m.Reason,
                                IsComplete = m.IsComplete
                            }).ToList(),
                            OverdueMilestones = item.OverdueMilestones.Select(m => new MilestonePrintModel
                            {
                                Name = m.Name,
                                StartDate = m.StartDate,
                                PlannedDate = m.PlannedDate,
                                Progress = m.Progress,
                                Status = m.Status,
                                Reason = m.Reason,
                                IsComplete = m.IsComplete
                            }).ToList(),
                            GeneralWasteTon = item.GeneralWasteTon,
                            RubbleM3 = item.RubbleM3,
                            ScrapMetalsTon = item.ScrapMetalsTon,
                            AsbestosTon = item.AsbestosTon,
                            StatusSummary = item.StatusSummary,
                            VendorReportRows = vendors.Select(r => new ProjectReportPrintVendorRow
                            {
                                VendorName = r.VendorName,
                                Scope = r.Scope,
                                SafetyApproved = r.SafetyApproved,
                                AppScore = r.AppScore,
                                Audit1 = r.Audit1,
                                Audit2 = r.Audit2,
                                Audit3 = r.Audit3
                            }).ToList(),
                            VariationOrders = variations,
                            IncidentPhotoPaths = localPhotoPaths
                        };

                        var path = await _pdfService.GenerateProjectReportPdfAsync(model);
                        var destPath = Path.Combine(runFolder, Path.GetFileName(path));
                        File.Copy(path, destPath, true);

                        // Upload generated PDF to server history
                        var reportName = $"{item.ProjectSummary.Name} - Week {item.WeekNumber}";
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            await _projectReportService.UploadReportAsync(item.ProjectSummary.Id, item.WeekNumber, reportName, fs, Path.GetFileName(path));
                        }

                        item.GenerationStatus = "Completed";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate report for project {ProjectName}", item.ProjectSummary.Name);
                        item.GenerationStatus = "Failed";
                    }

                    BatchProgress = (double)(i + 1) / selected.Count * 100;
                }

                BatchStatusText = "Batch report generation complete.";
                NotifySuccess("Generation Complete", $"Generated {selected.Count(s => s.GenerationStatus == "Completed")} reports in:\n{runFolder}");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(runFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run batch generation");
                NotifyError("Error", "Batch generation encountered a critical error: " + ex.Message);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private async Task<double> FetchSafeWorkingHoursAsync(Guid projectId)
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
                    return records
                        .Where(r => r.ProjectId == projectId && r.Status == AttendanceStatus.Present)
                        .Sum(r => r.HoursWorked);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch attendance records for project {ProjectId} in run.", projectId);
            }
            return 0;
        }

        private async Task<List<VendorReportRow>> BuildVendorReportAsync(Guid projectId, string projectName, List<ProjectTask> nonGroupTasks)
        {
            var rows = new List<VendorReportRow>();
            try
            {
                var allAudits = await _healthSafetyService.GetAuditsAsync();
                var projectAudits = allAudits
                    .Where(a => a.SiteName != null && a.SiteName.Contains(projectName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.Date)
                    .ToList();

                string audit1 = projectAudits.Count > 0 ? $"{projectAudits[0].ActualScore}%" : "-";
                string audit2 = projectAudits.Count > 1 ? $"{projectAudits[1].ActualScore}%" : "-";
                string audit3 = projectAudits.Count > 2 ? $"{projectAudits[2].ActualScore}%" : "-";

                rows.Add(new VendorReportRow
                {
                    VendorName = "Orange Circle Construction",
                    Scope = "Primary Contractor",
                    SafetyApproved = "Yes",
                    AppScore = "100%",
                    Audit1 = audit1,
                    Audit2 = audit2,
                    Audit3 = audit3
                });

                var subbies = nonGroupTasks
                    .SelectMany(t => t.Assignments ?? new List<TaskAssignment>())
                    .Where(a => a.AssigneeType == AssigneeType.Contractor)
                    .Select(a => a.AssigneeName)
                    .Distinct()
                    .ToList();

                foreach (var name in subbies)
                {
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

                    rows.Add(new VendorReportRow
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
                _logger.LogWarning(ex, "Failed to build vendor report details for project {ProjectName}", projectName);
            }
            return rows;
        }

        private async Task<List<ProjectVariationOrder>> FetchVariationOrdersAsync(Guid projectId)
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
                var url = $"{baseUrl}api/ProjectVariationOrders?projectId={projectId}";

                var vOs = await client.GetFromJsonAsync<List<ProjectVariationOrder>>(url);
                if (vOs != null)
                {
                    return vOs;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch variation orders for project {ProjectId}.", projectId);
            }
            return new List<ProjectVariationOrder>();
        }

        private async Task<List<string>> FetchAndDownloadIncidentPhotosAsync(Guid projectId, string projectName)
        {
            var localPhotoPaths = new List<string>();
            try
            {
                var incidents = await _healthSafetyService.GetIncidentsAsync();
                var projectIncidents = incidents.Where(i => i.Location != null && i.Location.Contains(projectName, StringComparison.OrdinalIgnoreCase)).ToList();

                var photos = new List<IncidentPhotoDto>();
                foreach (var inc in projectIncidents)
                {
                    var detail = await _healthSafetyService.GetIncidentAsync(inc.Id);
                    if (detail?.Photos != null)
                    {
                        photos.AddRange(detail.Photos);
                    }
                }

                if (photos.Any())
                {
                    var tempPhotosDir = Path.Combine(Path.GetTempPath(), $"OCC_Report_Photos_{projectId}");
                    try
                    {
                        if (Directory.Exists(tempPhotosDir))
                        {
                            Directory.Delete(tempPhotosDir, true);
                        }
                    }
                    catch { }

                    Directory.CreateDirectory(tempPhotosDir);
                    using var client = _httpClientFactory.CreateClient();
                    var token = _authService.CurrentToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }

                    foreach (var photo in photos)
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
                            _logger.LogWarning(ex, "Failed to download photo {PhotoId}", photo.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch project incident photos for project {ProjectName}", projectName);
            }
            return localPhotoPaths;
        }

        [RelayCommand]
        public async Task CloseAsync()
        {
            // Save selected item changes on close
            if (SelectedItem != null)
            {
                await SelectedItem.SaveLocalReportDataAsync();
            }
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }

    public partial class ProjectReportRunItemViewModel : ObservableObject
    {
        private readonly IProjectService _projectService;
        private readonly IProjectReportService _projectReportService;
        private readonly ILogger<ProjectReportRunItemViewModel> _logger;

        [ObservableProperty] private ProjectSummaryDto _projectSummary;
        [ObservableProperty] private Project? _projectDetails;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private int _weekNumber;
        [ObservableProperty] private string _statusSummary = string.Empty;
        [ObservableProperty] private string _generalWasteTon = "0";
        [ObservableProperty] private string _rubbleM3 = "0";
        [ObservableProperty] private string _scrapMetalsTon = "0";
        [ObservableProperty] private string _asbestosTon = "0";
        [ObservableProperty] private double _powPercentRequired;
        [ObservableProperty] private int _delayDays;
        [ObservableProperty] private string _generationStatus = "Pending";

        // Dynamic Milestones
        [ObservableProperty] private ObservableCollection<MilestoneReportItem> _thisWeekMilestones = new();
        [ObservableProperty] private ObservableCollection<MilestoneReportItem> _overdueMilestones = new();
        [ObservableProperty] private bool _hasThisWeekMilestones;
        [ObservableProperty] private bool _noThisWeekMilestones = true;
        [ObservableProperty] private bool _hasOverdueMilestones;
        [ObservableProperty] private bool _noOverdueMilestones = true;

        public List<ProjectTask> LoadedTasks { get; private set; } = new();

        public ProjectReportRunItemViewModel(
            ProjectSummaryDto summary,
            IProjectService projectService,
            IProjectReportService projectReportService,
            ILogger<ProjectReportRunItemViewModel> logger)
        {
            _projectSummary = summary;
            _projectService = projectService;
            _projectReportService = projectReportService;
            _logger = logger;
        }

        public async Task LoadDetailsAsync()
        {
            try
            {
                ProjectDetails = await _projectService.GetProjectAsync(ProjectSummary.Id);
                if (ProjectDetails == null) return;

                var tasks = (await _projectService.GetProjectTasksAsync(ProjectSummary.Id)).ToList();
                var leafTasks = tasks.Where(t => !t.IsGroup).ToList();
                LoadedTasks = leafTasks;

                // Calculate week number based on Thursday-to-Thursday reporting cycle
                if (ProjectDetails.StartDate != default)
                {
                    var firstThursday = ProjectDetails.StartDate.Date;
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

                // Load draft details from backend
                var draft = await _projectReportService.GetDraftAsync(ProjectSummary.Id);
                if (draft != null)
                {
                    StatusSummary = draft.StatusSummary;
                    GeneralWasteTon = draft.GeneralWasteTon;
                    RubbleM3 = draft.RubbleM3;
                    ScrapMetalsTon = draft.ScrapMetalsTon;
                    AsbestosTon = draft.AsbestosTon;
                    PowPercentRequired = draft.PowPercentRequired;
                    DelayDays = draft.DelayDays;
                }
                else
                {
                    // Defaults
                    StatusSummary = ProjectDetails?.Description ?? string.Empty;
                    GeneralWasteTon = "0";
                    RubbleM3 = "0";
                    ScrapMetalsTon = "0";
                    AsbestosTon = "0";
                    PowPercentRequired = 0;
                    DelayDays = 0;
                }

                // Calculate Comprehensive POW progress
                var today = DateTime.Today;
                double totalLeafPlannedProgress = 0;
                foreach (var task in leafTasks)
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
                PowPercentRequired = leafTasks.Count > 0 ? totalLeafPlannedProgress / leafTasks.Count : 0;

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
                if (ProjectDetails != null && !string.IsNullOrEmpty(ProjectDetails.Name))
                {
                    projectTask = tasks.FirstOrDefault(t => t.Name.Trim().Equals(ProjectDetails.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (projectTask == null)
                    {
                        projectTask = tasks.FirstOrDefault(t => t.Name.Contains(ProjectDetails.Name, StringComparison.OrdinalIgnoreCase));
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load project run item details for project {ProjectId}", ProjectSummary.Id);
            }
        }

        public async Task SaveLocalReportDataAsync()
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

                var draft = new ProjectReportDraft
                {
                    ProjectId = ProjectSummary.Id,
                    StatusSummary = StatusSummary,
                    GeneralWasteTon = GeneralWasteTon,
                    RubbleM3 = RubbleM3,
                    ScrapMetalsTon = ScrapMetalsTon,
                    AsbestosTon = AsbestosTon,
                    PowPercentRequired = PowPercentRequired,
                    DelayDays = DelayDays,
                    OverdueMilestoneReasons = overdueMilestoneReasonsJson
                };

                await _projectReportService.SaveDraftAsync(ProjectSummary.Id, draft);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save report draft for project {ProjectName}", ProjectSummary.Name);
            }
        }

        public void AutoGenerateSummary()
        {
            if (ProjectDetails == null) return;

            var completedList = LoadedTasks?.Where(t => t.Status == "Completed" || t.Status == "Done" || t.PercentComplete == 100).Select(t => t.Name).Take(3).ToList() ?? new();
            var inProgressList = LoadedTasks?.Where(t => t.Status == "In Progress" || t.Status == "Started" || (t.PercentComplete > 0 && t.PercentComplete < 100)).Select(t => t.Name).Take(3).ToList() ?? new();
            var upcomingList = LoadedTasks?.Where(t => t.Status != "Completed" && t.Status != "Done" && t.PercentComplete == 0).Select(t => t.Name).Take(3).ToList() ?? new();

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
    }
}
