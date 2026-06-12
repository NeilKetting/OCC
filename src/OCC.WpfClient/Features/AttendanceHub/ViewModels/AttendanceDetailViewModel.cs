using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class AttendanceDetailViewModel : DetailViewModelBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly AttendanceRecord _originalRecord;
        private readonly AttendanceStatus _previousStatus;

        [ObservableProperty] private AttendanceRecord _editingRecord;
        [ObservableProperty] private string? _sickNoteFilePath;
        [ObservableProperty] private bool _hasSickNote;

        public AttendanceDetailViewModel(
            AttendanceRecord record,
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _originalRecord = record;
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _previousStatus = record.Status;

            Title = "EDIT ATTENDANCE RECORD";

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

            // 2. Save the attendance record
            await _attendanceService.UpdateAttendanceRecordAsync(EditingRecord);

            // 3. Deduct sick leave balance if status changed TO Sick (and wasn't already Sick)
            if (EditingRecord.Status == AttendanceStatus.Sick &&
                _previousStatus != AttendanceStatus.Sick &&
                EditingRecord.EmployeeId.HasValue)
            {
                try
                {
                    var emp = await _employeeService.GetEmployeeAsync(EditingRecord.EmployeeId.Value);
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
                        NotifySuccess("Record Updated",
                            $"Status changed to Sick. 1 sick day deducted from {emp.FirstName} {emp.LastName}'s balance ({emp.SickLeaveBalance:F1} days remaining).");
                    }
                }
                catch (Exception balEx)
                {
                    _logger.LogWarning(balEx, "Could not deduct sick leave balance for employee {Id}", EditingRecord.EmployeeId);
                    NotifySuccess("Record Updated", "Attendance record saved. Note: sick leave balance could not be updated automatically.");
                }
            }
            else
            {
                NotifySuccess("Saved", "Attendance record updated.");
            }
        }

        protected override Task ExecuteReloadAsync() => Task.CompletedTask;

        protected override string GetReportTitle() => "Attendance Record";
        protected override object GetReportItem() => EditingRecord;
    }
}
