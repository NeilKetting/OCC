using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class TrainingViewModel : ListViewModelBase<TrainingRecordViewModel>
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IDialogService _dialogService;
        private readonly IEmployeeService _employeeService;
        private readonly IExportService _exportService;
        private readonly ConnectionSettings _settings;

        [ObservableProperty] private int _expiryWarningDays = 30;
        [ObservableProperty] private string _categoryFilter = "All";

        private List<TrainingRecordViewModel> _allRecords = new();

        [ObservableProperty]
        private bool _isTrainerVisible = true;

        public override string ReportTitle => "Employee Training & Medical Records";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Employee", PropertyName = "EmployeeName", Width = 2.5 },
            new() { Header = "Topic", PropertyName = "TrainingTopic", Width = 2.5 },
            new() { Header = "Date", PropertyName = "DateCompleted", Width = 1 },
            new() { Header = "Expires", PropertyName = "ValidUntil", Width = 1 },
            new() { Header = "Status", PropertyName = "ValidityStatus", Width = 1.5 }
        };

        public ObservableCollection<string> Categories { get; } = new() { "All", "Training", "Medicals" };

        public TrainingEditorViewModel Editor { get; }

        public TrainingViewModel(
            IHealthSafetyService hseqService, 
            IDialogService dialogService, 
            IEmployeeService employeeService,
            IExportService exportService,
            ConnectionSettings settings,
            TrainingEditorViewModel editor,
            IPdfService pdfService) : base(pdfService)
        {
            _hseqService = hseqService;
            _dialogService = dialogService;
            _employeeService = employeeService;
            _exportService = exportService;
            _settings = settings;
            Editor = editor;
            Title = "Training";
            
            Editor.OnSaved = OnTrainingSaved;

            _ = LoadDataAsync();
        }

        // Design-time
        public TrainingViewModel() : base(null!)
        {
            _hseqService = null!;
            _dialogService = null!;
            _employeeService = null!;
            _exportService = null!;
            _settings = null!;
            Editor = new TrainingEditorViewModel();
        }

        public override async Task LoadDataAsync()
        {
            if (_hseqService == null) return;

            IsBusy = true;
            try
            {
                var summariesTask = _hseqService.GetTrainingSummariesAsync();
                var employeesTask = _employeeService.GetEmployeesAsync();

                await Task.WhenAll(summariesTask, employeesTask);

                var summaries = summariesTask.Result;
                var employeesDto = employeesTask.Result.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

                _allRecords = summaries.Select(r => new TrainingRecordViewModel(r)).ToList();
                FilterItems();
                
                var uniqueTrainers = summaries
                    .Where(r => !string.IsNullOrWhiteSpace(r.Trainer))
                    .Select(r => r.Trainer)
                    .Distinct();

                Editor.Initialize(employeesDto, uniqueTrainers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading training data: {ex.Message}");
                NotifyError("Error", "Failed to load training data.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected override void FilterItems()
        {
            IEnumerable<TrainingRecordViewModel> filtered = _allRecords;

            if (CategoryFilter == "Medicals")
            {
                filtered = filtered.Where(r => r.Summary.CertificateType == "Medicals");
            }
            else if (CategoryFilter == "Training")
            {
                filtered = filtered.Where(r => r.Summary.CertificateType != "Medicals");
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(r => 
                    (r.EmployeeName?.ToLower().Contains(query) ?? false) ||
                    (r.TrainingTopic?.ToLower().Contains(query) ?? false) ||
                    (r.Role?.ToLower().Contains(query) ?? false) ||
                    (r.Trainer?.ToLower().Contains(query) ?? false));
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<TrainingRecordViewModel>(result);
            TotalCount = result.Count;
        }

        partial void OnCategoryFilterChanged(string value)
        {
            FilterItems();
        }

        [RelayCommand]
        private async Task FilterExpiring()
        {
            IsBusy = true;
            try
            {
                var expiring = await _hseqService.GetExpiringTrainingAsync(ExpiryWarningDays);
                var vms = expiring.Select(r => new TrainingRecordViewModel(r)).ToList();
                Items = new ObservableCollection<TrainingRecordViewModel>(vms);
                TotalCount = Items.Count;
                NotifySuccess("Filter Applied", $"Found {expiring.Count()} records expiring within {ExpiryWarningDays} days.");
            }
            catch (Exception)
            {
                 NotifyError("Error", "Failed to filter records.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteRecord(TrainingRecordViewModel vm)
        {
            if (vm == null) return;
            
            var confirm = await _dialogService.ShowConfirmationAsync("Confirm Delete", $"Delete training record for {vm.EmployeeName}?");
            if (confirm)
            {
                var success = await _hseqService.DeleteTrainingRecordAsync(vm.Id);
                if (success)
                {
                    var record = _allRecords.FirstOrDefault(r => r.Id == vm.Id);
                    if (record != null) _allRecords.Remove(record);
                    FilterItems();
                    NotifySuccess("Success", "Record deleted.");
                }
            }
        }

        [RelayCommand]
        private void ToggleAdd()
        {
            if (Editor.IsOpen)
            {
                Editor.ClearForm();
            }
            else
            {
                Editor.OpenForAdd();
            }
        }

        [RelayCommand]
        private void CancelAdd()
        {
            Editor.ClearForm();
        }

        [RelayCommand]
        private async Task EditRecord(TrainingRecordViewModel vm)
        {
            if (vm == null) return;
            
            BusyText = "Loading record details...";
            IsBusy = true;
            var full = await _hseqService.GetTrainingRecordAsync(vm.Id);
            IsBusy = false;

            if (full != null)
            {
                Editor.OpenForEdit(full, Editor.Employees);
                NotifySuccess("Editing", $"Modifying record for {vm.EmployeeName}");
            }
            else
            {
                NotifyError("Error", "Could not load record details.");
            }
        }

        [RelayCommand]
        private async Task ViewCertificate(TrainingRecordViewModel vm)
        {
            if (vm == null || !vm.HasCertificate) return;
            
            var url = vm.Summary.CertificateUrl;
            
            if (url.StartsWith("/") || url.StartsWith("uploads"))
            {
                var baseUrl = _settings.ApiBaseUrl.TrimEnd('/');
                if (!url.StartsWith("/")) url = "/" + url;
                url = baseUrl + url;
            }

            try 
            {
                await _exportService.OpenFileAsync(url);
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Could not open certificate: " + ex.Message);
            }
        }

        private async Task OnTrainingSaved(HseqTrainingRecord record)
        {
            var summary = new HseqTrainingSummaryDto
            {
                Id = record.Id,
                EmployeeName = record.EmployeeName,
                TrainingTopic = record.TrainingTopic,
                CertificateType = record.CertificateType,
                DateCompleted = record.DateCompleted,
                ValidUntil = record.ValidUntil,
                Role = record.Role,
                CertificateUrl = record.CertificateUrl,
                Trainer = record.Trainer ?? string.Empty
            };

            var existing = _allRecords.FirstOrDefault(r => r.Id == record.Id);
            if (existing != null)
            {
                var index = _allRecords.IndexOf(existing);
                _allRecords[index] = new TrainingRecordViewModel(summary);
            }
            else
            {
                _allRecords.Insert(0, new TrainingRecordViewModel(summary));
            }
            
            FilterItems();
            await Task.CompletedTask;
        }
    }

    public class TrainingRecordViewModel : ObservableObject
    {
        public HseqTrainingSummaryDto Summary { get; }

        public TrainingRecordViewModel(HseqTrainingSummaryDto summary)
        {
            Summary = summary;
        }

        public Guid Id => Summary.Id;
        public string EmployeeName => Summary.EmployeeName;
        public string Role => Summary.Role;
        public string TrainingTopic => Summary.TrainingTopic;
        public DateTime DateCompleted => Summary.DateCompleted;
        public DateTime? ValidUntil => Summary.ValidUntil;
        public string Trainer => Summary.Trainer; 

        public string ValidityStatus
        {
            get
            {
                if (!ValidUntil.HasValue) return "No Expiry";
                
                var daysLeft = (ValidUntil.Value.Date - DateTime.Now.Date).Days;
                
                if (daysLeft < 0) return $"Expired ({Math.Abs(daysLeft)} days ago)";
                if (daysLeft == 0) return "Expires Today";
                if (daysLeft < 30) return $"Expires in {daysLeft} days";
                
                if (daysLeft > 365) 
                {
                    var years = Math.Round(daysLeft / 365.25, 1);
                    return $"{years} Years";
                }
                
                return $"{daysLeft} Days";
            }
        }

        public bool HasCertificate => !string.IsNullOrEmpty(Summary.CertificateUrl);
    }
}
