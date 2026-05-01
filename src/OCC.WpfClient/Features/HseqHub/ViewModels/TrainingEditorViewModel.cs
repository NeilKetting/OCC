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
using System.IO;
using System.Collections.Generic;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class TrainingEditorViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IDialogService _dialogService;

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
        private ObservableCollection<EmployeeSummaryDto> _employees = new();

        [ObservableProperty]
        private EmployeeSummaryDto? _selectedEmployee;

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
            IDialogService dialogService)
        {
            _hseqService = hseqService;
            _dialogService = dialogService;
            InitializeCertificateTypes();
        }

        // Design-time
        public TrainingEditorViewModel()
        {
            _hseqService = null!;
            _dialogService = null!;
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

        public void Initialize(IEnumerable<EmployeeSummaryDto> employees, IEnumerable<string> trainers)
        {
            Employees = new ObservableCollection<EmployeeSummaryDto>(employees);
            Trainers = new ObservableCollection<string>(trainers.OrderBy(t => t));
            ClearForm();
        }

        public void OpenForAdd()
        {
            ClearForm();
            IsOpen = true;
        }

        public void OpenForEdit(HseqTrainingRecord record, IEnumerable<EmployeeSummaryDto> employees)
        {
            Employees = new ObservableCollection<EmployeeSummaryDto>(employees);
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

        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)
        {
            if (value != null)
            {
                NewRecord.EmployeeName = value.DisplayName;
                NewRecord.Role = value.Role.ToString();
                NewRecord.EmployeeId = value.Id;
            }
        }

        [RelayCommand]
        public void UploadCertificate()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Certificate",
                Filter = "Documents|*.pdf;*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                NewRecord.CertificateUrl = dialog.FileName;
                CertificateFileName = Path.GetFileName(dialog.FileName);
                NotifySuccess("File Selected", CertificateFileName);
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
                NotifyError("Validation", "Employee Name and Certificate Type are required.");
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
                        NotifyError("Upload Failed", "Could not upload certificate. Saving text only.");
                        System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
                    }
                }

                NewRecord.TrainingTopic = NewRecord.CertificateType; 

                if (IsEditMode && _editingRecord != null)
                {
                    var success = await _hseqService.UpdateTrainingRecordAsync(NewRecord);
                    if (success)
                    {
                        NotifySuccess("Updated", "Training record updated.");
                        if (OnSaved != null) await OnSaved(NewRecord);
                        ClearForm();
                    }
                    else
                    {
                        NotifyError("Error", "Failed to update record.");
                    }
                }
                else
                {
                    var created = await _hseqService.CreateTrainingRecordAsync(NewRecord);
                    if (created != null)
                    {
                        NotifySuccess("Saved", "Training record added.");
                        if (OnSaved != null) await OnSaved(created);
                        ClearForm();
                    }
                }
            }
            catch(Exception ex)
            {
                NotifyError("Error", "Failed to save record.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
