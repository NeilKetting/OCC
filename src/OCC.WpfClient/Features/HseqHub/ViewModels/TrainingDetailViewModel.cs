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
    public partial class TrainingDetailViewModel : OverlayViewModel
    {
        private readonly IHealthSafetyService _hseqService;

        [ObservableProperty]
        private HseqTrainingRecord _record = new() 
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

        public TrainingDetailViewModel(IHealthSafetyService hseqService)
        {
            _hseqService = hseqService;
            Title = "Training Record";
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

        public void Initialize(IEnumerable<EmployeeSummaryDto> employees, IEnumerable<string> trainers, HseqTrainingRecord? record = null)
        {
            Employees = new ObservableCollection<EmployeeSummaryDto>(employees);
            Trainers = new ObservableCollection<string>(trainers.OrderBy(t => t));
            
            if (record != null)
            {
                IsEditMode = true;
                Record = record;
                SelectedEmployee = Employees.FirstOrDefault(e => e.Id == record.EmployeeId);
                CertificateFileName = string.IsNullOrEmpty(record.CertificateUrl) 
                    ? "No file selected" 
                    : Path.GetFileName(record.CertificateUrl);
            }
            else
            {
                IsEditMode = false;
                Record = new HseqTrainingRecord
                {
                    DateCompleted = DateTime.Now,
                    ValidUntil = DateTime.Now
                };
            }
        }

        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)
        {
            if (value != null)
            {
                Record.EmployeeName = value.DisplayName;
                Record.Role = value.Role.ToString();
                Record.EmployeeId = value.Id;
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
                Record.CertificateUrl = dialog.FileName;
                CertificateFileName = Path.GetFileName(dialog.FileName);
                NotifySuccess("File Selected", CertificateFileName);
            }
        }

        [RelayCommand]
        public async Task SaveTraining()
        {
            if (string.IsNullOrWhiteSpace(Record.EmployeeName) || string.IsNullOrWhiteSpace(Record.CertificateType))
            {
                NotifyError("Validation", "Employee Name and Certificate Type are required.");
                return;
            }

            IsBusy = true;
            try
            {
                if (!string.IsNullOrEmpty(Record.CertificateUrl) && File.Exists(Record.CertificateUrl))
                {
                    try
                    {
                        using var stream = File.OpenRead(Record.CertificateUrl);
                        var fileName = Path.GetFileName(Record.CertificateUrl);
                        var serverUrl = await _hseqService.UploadCertificateAsync(stream, fileName);
                        
                        if (!string.IsNullOrEmpty(serverUrl))
                        {
                            Record.CertificateUrl = serverUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        NotifyError("Upload Failed", "Could not upload certificate. Saving text only.");
                        System.Diagnostics.Debug.WriteLine($"Upload error: {ex.Message}");
                    }
                }

                Record.TrainingTopic = Record.CertificateType; 

                if (IsEditMode)
                {
                    var success = await _hseqService.UpdateTrainingRecordAsync(Record);
                    if (success)
                    {
                        NotifySuccess("Updated", "Training record updated.");
                        Close(Record);
                    }
                    else
                    {
                        NotifyError("Error", "Failed to update record.");
                    }
                }
                else
                {
                    var created = await _hseqService.CreateTrainingRecordAsync(Record);
                    if (created != null)
                    {
                        NotifySuccess("Saved", "Training record added.");
                        Close(created);
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
