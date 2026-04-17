using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using OCC.Client.ViewModels.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class TrainingEditorViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly IRepository<Employee> _employeeRepository;

        [ObservableProperty]
        private HseqTrainingRecord _newRecord = new() 
        { 
            DateCompleted = DateTime.Now, 
            ValidUntil = DateTime.Now 
        };

        [ObservableProperty]
        private ObservableCollection<string> _certificateTypes = new();

        [ObservableProperty]
        private ObservableCollection<string> _trainers = new();

        [ObservableProperty]
        private ObservableCollection<Employee> _employees = new();

        [ObservableProperty]
        private Employee? _selectedEmployee;

        [ObservableProperty]
        private string _certificateFileName = "No file selected";

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private bool _isOpen;

        private HseqTrainingRecord? _editingRecord;

        public Func<HseqTrainingRecord, Task>? OnSaved { get; set; }

        public TrainingEditorViewModel(
            IHealthSafetyService hseqService, 
            IDialogService dialogService, 
            IToastService toastService,
            IRepository<Employee> employeeRepository)
        {
            _hseqService = hseqService;
            _dialogService = dialogService;
            _toastService = toastService;
            _employeeRepository = employeeRepository;
            InitializeCertificateTypes();
        }

        private void InitializeCertificateTypes()
        {
            CertificateTypes = new ObservableCollection<string>
            {
                "Medicals", "First Aid Level 1", "First Aid Level 2", "First Aid Level 3",
                "SHE Representative", "Basic Fire Fighting", "Advanced Fire Fighting",
                "HIRA (Hazard Identification & Risk Assessment)", "Scaffolding Erector",
                "Scaffolding Inspector", "Working at Heights", "Fall Protection Planner",
                "Confined Space Entry", "Incident Investigation", "Legal Liability",
                "Construction Regulations", "Excavation Supervisor", "Demolition Supervisor",
                "PTW", "Emergency Evacuation", "Stacking and Storing"
            };
        }

        public void Initialize(IEnumerable<Employee> employees, IEnumerable<string> trainers)
        {
            Employees = new ObservableCollection<Employee>(employees);
            Trainers = new ObservableCollection<string>(trainers.OrderBy(t => t));
            ClearForm();
        }

        public void OpenForAdd()
        {
            ClearForm();
            IsOpen = true;
        }

        public void OpenForEdit(HseqTrainingRecord record, IEnumerable<Employee> employees)
        {
            Employees = new ObservableCollection<Employee>(employees);
            _editingRecord = record;
            IsEditMode = true;
            IsOpen = true;

            SelectedEmployee = Employees.FirstOrDefault(e => e.Id == record.EmployeeId);

            NewRecord = new HseqTrainingRecord
            {
                Id = record.Id,
                EmployeeId = record.EmployeeId,
                EmployeeName = record.EmployeeName,
                Role = record.Role,
                TrainingTopic = record.TrainingTopic,
                DateCompleted = record.DateCompleted,
                ValidUntil = record.ValidUntil,
                Trainer = record.Trainer,
                CertificateUrl = record.CertificateUrl,
                CertificateType = record.CertificateType,
                ExpiryWarningDays = record.ExpiryWarningDays,
                RowVersion = record.RowVersion,
                CreatedAtUtc = record.CreatedAtUtc,
                CreatedBy = record.CreatedBy,
                UpdatedAtUtc = record.UpdatedAtUtc,
                UpdatedBy = record.UpdatedBy,
                IsActive = record.IsActive
            };

            CertificateFileName = string.IsNullOrEmpty(record.CertificateUrl) 
                ? "No file selected" 
                : Path.GetFileName(record.CertificateUrl);
        }

        partial void OnSelectedEmployeeChanged(Employee? value)
        {
            if (value != null)
            {
                var existingId = NewRecord?.Id ?? Guid.Empty;
                var existingTopic = NewRecord?.TrainingTopic ?? string.Empty;
                var existingDate = (NewRecord?.DateCompleted == default || NewRecord?.DateCompleted == DateTime.MinValue) ? DateTime.Now : NewRecord!.DateCompleted;
                var existingValid = (NewRecord?.ValidUntil == default || NewRecord?.ValidUntil == DateTime.MinValue) ? DateTime.Now : NewRecord!.ValidUntil;
                var existingTrainer = NewRecord?.Trainer ?? string.Empty;
                var existingCertUrl = NewRecord?.CertificateUrl ?? string.Empty;
                var existingCertType = NewRecord?.CertificateType ?? string.Empty;
                var existingWarning = NewRecord?.ExpiryWarningDays ?? 30;
                var existingRow = NewRecord?.RowVersion ?? Array.Empty<byte>();
                var existingCreated = NewRecord?.CreatedAtUtc ?? DateTime.UtcNow;
                var existingCreatedBy = NewRecord?.CreatedBy ?? "System";
                var existingUpdated = NewRecord?.UpdatedAtUtc;
                var existingUpdatedBy = NewRecord?.UpdatedBy ?? string.Empty;
                var existingActive = NewRecord?.IsActive ?? true;

                NewRecord = new HseqTrainingRecord
                {
                    Id = existingId,
                    EmployeeName = value.DisplayName,
                    Role = value.Role.ToString(),
                    EmployeeId = value.Id,
                    TrainingTopic = existingTopic,
                    DateCompleted = existingDate,
                    ValidUntil = existingValid,
                    Trainer = existingTrainer,
                    CertificateUrl = existingCertUrl,
                    CertificateType = existingCertType,
                    ExpiryWarningDays = existingWarning,
                    RowVersion = existingRow,
                    CreatedAtUtc = existingCreated,
                    CreatedBy = existingCreatedBy,
                    UpdatedAtUtc = existingUpdated,
                    UpdatedBy = existingUpdatedBy,
                    IsActive = existingActive
                };
            }
        }

        [RelayCommand]
        public async Task UploadCertificate()
        {
            try
            {
                var path = await _dialogService.PickFileAsync("Select Certificate", new[] { "pdf", "jpg", "jpeg", "png" });
                if (!string.IsNullOrEmpty(path))
                {
                    NewRecord.CertificateUrl = path;
                    CertificateFileName = Path.GetFileName(path);
                    _toastService.ShowSuccess("File Selected", CertificateFileName);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to select file.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        public void ClearForm()
        {
            _editingRecord = null;
            IsEditMode = false;
            SelectedEmployee = null;
            CertificateFileName = "No file selected";
            NewRecord = new HseqTrainingRecord
            {
                DateCompleted = DateTime.Now,
                ValidUntil = DateTime.Now
            };
            IsOpen = false;
        }

        [RelayCommand]
        public async Task SaveTraining()
        {
            if (string.IsNullOrWhiteSpace(NewRecord.EmployeeName) || string.IsNullOrWhiteSpace(NewRecord.CertificateType))
            {
                _toastService.ShowError("Validation", "Employee Name and Certificate Type are required.");
                return;
            }

            IsBusy = true;
            try
            {
                if (!string.IsNullOrEmpty(NewRecord.CertificateUrl) && File.Exists(NewRecord.CertificateUrl))
                {
                    try
                    {
                        using var stream = File.OpenRead(NewRecord.CertificateUrl);
                        var fileName = Path.GetFileName(NewRecord.CertificateUrl);
                        var serverUrl = await _hseqService.UploadCertificateAsync(stream, fileName);
                        
                        if (!string.IsNullOrEmpty(serverUrl))
                        {
                            NewRecord.CertificateUrl = serverUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _toastService.ShowError("Upload Failed", "Could not upload certificate. Saving text only.");
                        System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
                    }
                }

                NewRecord.TrainingTopic = NewRecord.CertificateType; 

                if (IsEditMode && _editingRecord != null)
                {
                    var success = await _hseqService.UpdateTrainingRecordAsync(NewRecord);
                    if (success)
                    {
                        _toastService.ShowSuccess("Updated", "Training record updated.");
                        if (OnSaved != null) await OnSaved(NewRecord);
                        ClearForm();
                    }
                    else
                    {
                        _toastService.ShowError("Error", "Failed to update record.");
                    }
                }
                else
                {
                    var created = await _hseqService.CreateTrainingRecordAsync(NewRecord);
                    if (created != null)
                    {
                        _toastService.ShowSuccess("Saved", "Training record added.");
                        if (OnSaved != null) await OnSaved(created);
                        ClearForm();
                    }
                }
            }
            catch(Exception)
            {
                _toastService.ShowError("Error", "Failed to save record.");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
