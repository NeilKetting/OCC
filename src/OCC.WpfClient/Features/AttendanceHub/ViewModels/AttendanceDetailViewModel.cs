using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class AttendanceDetailViewModel : DetailViewModelBase
    {
        private static readonly Guid SelectSiteId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly IProjectService _projectService;
        private readonly AttendanceRecord _originalRecord;
        private readonly AttendanceStatus _previousStatus;

        [ObservableProperty] private AttendanceRecord _editingRecord;
        [ObservableProperty] private string? _sickNoteFilePath;
        [ObservableProperty] private bool _hasSickNote;
        [ObservableProperty] private bool _isNew;
        [ObservableProperty] private List<OCC.Shared.DTOs.EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private OCC.Shared.DTOs.EmployeeSummaryDto? _selectedEmployee;

        [ObservableProperty] private List<OCC.Shared.DTOs.ProjectSummaryDto> _projects = new();
        [ObservableProperty] private OCC.Shared.DTOs.ProjectSummaryDto? _selectedProject;
        [ObservableProperty] private string? _customSiteName;
        [ObservableProperty] private bool _isCustomSiteVisible;

        public AttendanceStatus Status
        {
            get => EditingRecord.Status;
            set
            {
                if (EditingRecord.Status != value)
                {
                    EditingRecord.Status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(IsProjectSelectorVisible));
                }
            }
        }

        public bool IsProjectSelectorVisible => 
            Status == AttendanceStatus.Present || 
            Status == AttendanceStatus.Late || 
            Status == AttendanceStatus.LeaveEarly;

        public TimeSpan? CheckInTimeSpan
        {
            get => EditingRecord.CheckInTime?.TimeOfDay;
            set
            {
                if (value.HasValue)
                {
                    EditingRecord.CheckInTime = EditingRecord.Date.Date.Add(value.Value);
                }
                else
                {
                    EditingRecord.CheckInTime = null;
                }
                OnPropertyChanged(nameof(CheckInTimeSpan));
            }
        }

        public TimeSpan? CheckOutTimeSpan
        {
            get => EditingRecord.CheckOutTime?.TimeOfDay;
            set
            {
                if (value.HasValue)
                {
                    EditingRecord.CheckOutTime = EditingRecord.Date.Date.Add(value.Value);
                }
                else
                {
                    EditingRecord.CheckOutTime = null;
                }
                OnPropertyChanged(nameof(CheckOutTimeSpan));
            }
        }

        public AttendanceDetailViewModel(
            AttendanceRecord record,
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IProjectService projectService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _originalRecord = record;
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _projectService = projectService;
            _previousStatus = record.Status;
            _isNew = record.Id == Guid.Empty;

            Title = _isNew ? "CREATE ATTENDANCE RECORD" : "EDIT ATTENDANCE RECORD";

            _editingRecord = new AttendanceRecord
            {
                Id = record.Id,
                EmployeeId = record.EmployeeId,
                Date = record.Date,
                CheckInTime = record.CheckInTime,
                CheckOutTime = record.CheckOutTime,
                Status = record.Status,
                Branch = record.Branch,
                Notes = record.Notes,
                HoursWorked = record.HoursWorked,
                DoctorsNoteImagePath = record.DoctorsNoteImagePath,
                RowVersion = record.RowVersion
            };

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var list = await _employeeService.GetEmployeesAsync();
                Employees = list.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

                var projects = await _projectService.GetProjectSummariesAsync(includeDeleted: false);
                var pList = new List<OCC.Shared.DTOs.ProjectSummaryDto>
                {
                    new OCC.Shared.DTOs.ProjectSummaryDto { Id = SelectSiteId, Name = "-- Please Select a Site --" },
                    new OCC.Shared.DTOs.ProjectSummaryDto { Id = Guid.Empty, Name = "Other (Specify)..." }
                };
                pList.AddRange(projects.OrderBy(p => p.Name));
                Projects = pList;

                if (IsNew)
                {
                    SelectedEmployee = Employees.FirstOrDefault();
                    Status = AttendanceStatus.Present;
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == SelectSiteId);
                }
                else
                {
                    SelectedEmployee = Employees.FirstOrDefault(e => e.Id == EditingRecord.EmployeeId);
                    Status = EditingRecord.Status;
                    
                    if (EditingRecord.ProjectId.HasValue)
                    {
                        SelectedProject = Projects.FirstOrDefault(p => p.Id == EditingRecord.ProjectId.Value);
                    }
                    else if (IsProjectSelectorVisible && !string.IsNullOrEmpty(EditingRecord.CustomSite))
                    {
                        SelectedProject = Projects.FirstOrDefault(p => p.Id == Guid.Empty);
                        CustomSiteName = EditingRecord.CustomSite;
                    }
                    else if (IsProjectSelectorVisible && !string.IsNullOrEmpty(EditingRecord.Notes))
                    {
                        // Fallback logic for legacy records where project info was stored in Notes
                        var isSystemNote = EditingRecord.Notes.Contains("Auto Clock-In", StringComparison.OrdinalIgnoreCase) || 
                                           EditingRecord.Notes.Contains("Auto Clock-Out", StringComparison.OrdinalIgnoreCase) ||
                                           EditingRecord.Notes.Contains("generated by system", StringComparison.OrdinalIgnoreCase);

                        if (isSystemNote)
                        {
                            SelectedProject = Projects.FirstOrDefault(p => p.Id == SelectSiteId);
                        }
                        else
                        {
                            var matchedProj = Projects.FirstOrDefault(p => p.Id != Guid.Empty && p.Id != SelectSiteId && string.Equals(p.Name, EditingRecord.Notes, StringComparison.OrdinalIgnoreCase));
                            if (matchedProj != null)
                            {
                                SelectedProject = matchedProj;
                            }
                            else
                            {
                                SelectedProject = Projects.FirstOrDefault(p => p.Id == Guid.Empty);
                                CustomSiteName = EditingRecord.Notes;
                            }
                        }
                    }
                    else
                    {
                        SelectedProject = Projects.FirstOrDefault(p => p.Id == SelectSiteId);
                    }

                    OnPropertyChanged(nameof(CheckInTimeSpan));
                    OnPropertyChanged(nameof(CheckOutTimeSpan));
                }

                // Force a property changed notification for all project-related properties to ensure UI sync after async loading
                OnPropertyChanged(nameof(IsProjectSelectorVisible));
                IsCustomSiteVisible = SelectedProject?.Id == Guid.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading initialization data for attendance detail");
            }
        }

        partial void OnSelectedEmployeeChanged(OCC.Shared.DTOs.EmployeeSummaryDto? value)
        {
            if (value != null)
            {
                EditingRecord.EmployeeId = value.Id;
                EditingRecord.Branch = value.Branch;

                if (IsNew)
                {
                    // Default shift times based on employee record or branch defaults
                    var shiftStart = value.ShiftStartTime ?? new TimeSpan(7, 0, 0);
                    var shiftEnd = value.ShiftEndTime ?? 
                        (string.Equals(value.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) 
                            ? new TimeSpan(16, 30, 0) 
                            : new TimeSpan(16, 45, 0));

                    EditingRecord.CheckInTime = EditingRecord.Date.Date.Add(shiftStart);
                    EditingRecord.CheckOutTime = EditingRecord.Date.Date.Add(shiftEnd);
                    OnPropertyChanged(nameof(CheckInTimeSpan));
                    OnPropertyChanged(nameof(CheckOutTimeSpan));
                }
            }
        }

        partial void OnSelectedProjectChanged(OCC.Shared.DTOs.ProjectSummaryDto? value)
        {
            IsCustomSiteVisible = value?.Id == Guid.Empty;
            if (!IsCustomSiteVisible)
            {
                CustomSiteName = null;
            }
        }

        [RelayCommand]
        private void UploadSickNote()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Sick Note / Doctor's Certificate",
                Filter = "Documents|*.pdf;*.jpg;*.jpeg;*.png;*.bmp|PDF Files|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                SickNoteFilePath = dialog.FileName;
                HasSickNote = true;
            }
        }

        protected override async Task ExecuteSaveAsync()
        {
            // 1. Upload sick note if provided
            if (!string.IsNullOrEmpty(SickNoteFilePath))
            {
                var serverPath = await _attendanceService.UploadSickNoteAsync(SickNoteFilePath);
                if (!string.IsNullOrEmpty(serverPath))
                    EditingRecord.DoctorsNoteImagePath = serverPath;
            }

            // Align check-in and check-out dates to the selected record Date
            if (EditingRecord.CheckInTime.HasValue)
            {
                EditingRecord.CheckInTime = EditingRecord.Date.Date.Add(EditingRecord.CheckInTime.Value.TimeOfDay);
            }
            if (EditingRecord.CheckOutTime.HasValue)
            {
                EditingRecord.CheckOutTime = EditingRecord.Date.Date.Add(EditingRecord.CheckOutTime.Value.TimeOfDay);
            }

            // Map project and custom site properties
            if (IsProjectSelectorVisible && SelectedProject != null)
            {
                if (SelectedProject.Id == Guid.Empty) // Other
                {
                    EditingRecord.ProjectId = null;
                    EditingRecord.CustomSite = CustomSiteName;
                }
                else if (SelectedProject.Id == SelectSiteId)
                {
                    EditingRecord.ProjectId = null;
                    EditingRecord.CustomSite = null;
                }
                else // Real project
                {
                    EditingRecord.ProjectId = SelectedProject.Id;
                    EditingRecord.CustomSite = null;
                }
            }
            else
            {
                EditingRecord.ProjectId = null;
                EditingRecord.CustomSite = null;
            }

            // 2. Save the attendance record
            if (IsNew)
            {
                await _attendanceService.CreateAttendanceRecordAsync(EditingRecord);
            }
            else
            {
                await _attendanceService.UpdateAttendanceRecordAsync(EditingRecord);
            }

            // 3. Deduct sick leave balance if status changed TO Sick (and wasn't already Sick)
            bool shouldDeductSickLeave = EditingRecord.Status == AttendanceStatus.Sick &&
                (IsNew || _previousStatus != AttendanceStatus.Sick) &&
                EditingRecord.EmployeeId.HasValue;

            if (shouldDeductSickLeave)
            {
                try
                {
                    var emp = await _employeeService.GetEmployeeAsync(EditingRecord.EmployeeId!.Value);
                    if (emp != null)
                    {
                        emp.SickLeaveBalance = Math.Max(0, emp.SickLeaveBalance - 1);
                        var updateEmp = new OCC.Shared.Models.Employee
                        {
                            Id = emp.Id,
                            FirstName = emp.FirstName,
                            LastName = emp.LastName,
                            EmployeeNumber = emp.EmployeeNumber ?? string.Empty,
                            IdNumber = emp.IdNumber,
                            Email = emp.Email,
                            Phone = emp.Phone,
                            Branch = emp.Branch,
                            Role = emp.Role,
                            Status = emp.Status,
                            HourlyRate = emp.HourlyRate,
                            SickLeaveBalance = emp.SickLeaveBalance,
                            AnnualLeaveBalance = emp.AnnualLeaveBalance,
                            ShiftStartTime = emp.ShiftStartTime,
                            ShiftEndTime = emp.ShiftEndTime,
                            RowVersion = emp.RowVersion
                        };
                        await _employeeService.UpdateEmployeeAsync(updateEmp);
                        NotifySuccess(IsNew ? "Record Created" : "Record Updated",
                            $"Status set to Sick. 1 sick day deducted from {emp.FirstName} {emp.LastName}'s balance ({emp.SickLeaveBalance:F1} days remaining).");
                    }
                }
                catch (Exception balEx)
                {
                    _logger.LogWarning(balEx, "Could not deduct sick leave balance for employee {Id}", EditingRecord.EmployeeId);
                    NotifySuccess(IsNew ? "Record Created" : "Record Updated",
                        $"Attendance record {(IsNew ? "created" : "saved")}. Note: sick leave balance could not be updated automatically.");
                }
            }
            else
            {
                NotifySuccess("Saved", IsNew ? "Attendance record created." : "Attendance record updated.");
            }
        }

        protected override Task ExecuteReloadAsync() => Task.CompletedTask;

        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();
            HasErrors = false;

            if (SelectedEmployee == null)
            {
                ValidationErrors.Add("Employee is required.");
            }

            if (IsProjectSelectorVisible)
            {
                if (SelectedProject == null || SelectedProject.Id == SelectSiteId)
                {
                    ValidationErrors.Add("Please select a project/site.");
                }
                else if (SelectedProject.Id == Guid.Empty && string.IsNullOrWhiteSpace(CustomSiteName))
                {
                    ValidationErrors.Add("Please specify the custom site location.");
                }
            }

            if (ValidationErrors.Any())
            {
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }

            return true;
        }

        protected override string GetReportTitle() => "Attendance Record";
        protected override object GetReportItem() => EditingRecord;
    }
}
