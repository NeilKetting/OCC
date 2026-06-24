using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class AttendanceImportRow : ObservableObject
    {
        public string RawEmployeeName { get; set; } = string.Empty;
        public string RawSiteName { get; set; } = string.Empty;

        [ObservableProperty] private EmployeeSummaryDto? _selectedEmployee;
        [ObservableProperty] private DateTime _date;
        [ObservableProperty] private ProjectSummaryDto? _selectedProject;
        [ObservableProperty] private string? _customSite;
        [ObservableProperty] private AttendanceStatus _status;
        [ObservableProperty] private TimeSpan? _checkInTime;
        [ObservableProperty] private TimeSpan? _checkOutTime;
        [ObservableProperty] private string _statusMessage = "Ready";
        [ObservableProperty] private bool _isValid = true;
        [ObservableProperty] private bool _isCustomSiteEnabled;

        public Func<Guid, DateTime, bool>? CheckExistingCallback { get; set; }

        public AttendanceImportRow()
        {
        }

        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)
        {
            Validate();
        }

        partial void OnDateChanged(DateTime value)
        {
            Validate();
        }

        partial void OnSelectedProjectChanged(ProjectSummaryDto? value)
        {
            IsCustomSiteEnabled = value?.Id == Guid.Empty;
            if (!IsCustomSiteEnabled)
            {
                CustomSite = null;
            }
            Validate();
        }

        partial void OnStatusChanged(AttendanceStatus value)
        {
            Validate();
        }

        public void Validate()
        {
            if (SelectedEmployee == null)
            {
                StatusMessage = "Employee not matched";
                IsValid = false;
                return;
            }

            if (CheckExistingCallback != null && CheckExistingCallback(SelectedEmployee.Id, Date))
            {
                StatusMessage = "Record already exists for this date";
                IsValid = false;
                return;
            }

            if (Status == AttendanceStatus.Present || Status == AttendanceStatus.Late || Status == AttendanceStatus.LeaveEarly)
            {
                if (SelectedProject == null)
                {
                    StatusMessage = "Project/Site required for Present status";
                    IsValid = false;
                    return;
                }
                if (SelectedProject.Id == Guid.Empty && string.IsNullOrWhiteSpace(CustomSite))
                {
                    StatusMessage = "Specify custom site name";
                    IsValid = false;
                    return;
                }
            }

            StatusMessage = "Ready to import";
            IsValid = true;
        }
    }
}
