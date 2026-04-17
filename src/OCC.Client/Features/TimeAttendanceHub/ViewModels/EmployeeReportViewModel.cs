using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.Features.EmployeeHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class EmployeeReportViewModel : ViewModelBase
    {
        private readonly ITimeService _timeService;
        private readonly IExportService _exportService;
        private readonly IHolidayService _holidayService;

        public Employee Employee { get; }

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<HistoryRecordViewModel> _records = new();

        [ObservableProperty]
        private double _totalHours;

        [ObservableProperty]
        private double _totalOvertime;

        [ObservableProperty]
        private double _totalOvertime15;

        [ObservableProperty]
        private double _totalOvertime20;

        [ObservableProperty]
        private int _totalLates;

        [ObservableProperty]
        private int _totalAbsences;

        [ObservableProperty]
        private decimal _totalPay;

        [ObservableProperty]
        private decimal _totalNormalPay;

        [ObservableProperty]
        private bool _isLoading;
        
        [ObservableProperty]
        private decimal _totalOvertimePay15;

        [ObservableProperty]
        private decimal _totalOvertimePay20;

        private readonly IPdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IEmployeeService _employeeService;

        public EmployeeReportViewModel(
            Employee employee,
            ITimeService timeService,
            IExportService exportService,
            IHolidayService holidayService,
            IPdfService pdfService,
            IDialogService dialogService,
            IEmployeeService employeeService)
        {
            Employee = employee;
            _timeService = timeService;
            _exportService = exportService;
            _holidayService = holidayService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _employeeService = employeeService;

            _ = LoadData();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadData();
        }

        partial void OnStartDateChanged(DateTime value) { _ = LoadData(); }
        partial void OnEndDateChanged(DateTime value) { _ = LoadData(); }

        public async Task LoadData()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var s = StartDate;
                var e = EndDate;

                // 1. Fetch Attendance for range
                var attendanceEnumerable = await _timeService.GetAttendanceByRangeAsync(s, e);
                
                // Filter for THIS employee
                var empAttendance = attendanceEnumerable
                                    .Where(a => a.EmployeeId == Employee.Id)
                                    .ToList();

                var list = new List<HistoryRecordViewModel>();
                
                // Process every day in the range to catch Absences
                var current = s.Date;
                var end = e.Date;
                if (end > DateTime.Today) end = DateTime.Today; // Don't project into future

                // We need to handle the case where "To" date is in the future, 
                // but we only show absences up to Today. 
                // However, if we have records in the future (unlikely but possible), show them?
                // Standard logic: Loop from Start to End.
                
                // Create lookup for performance
                var attendanceLookup = empAttendance.ToLookup(a => a.Date.Date);

                // Use the loop end as selected end date, but cap "Absence Creation" at Today
                for (var d = s.Date; d <= e.Date; d = d.AddDays(1))
                {
                    if (attendanceLookup.Contains(d))
                    {
                        // Add actual records
                        foreach(var rec in attendanceLookup[d])
                        {
                            list.Add(new HistoryRecordViewModel(rec, Employee, _holidayService));
                        }
                    }
                    else
                    {
                        // No record found. Check if we should insert an "Absent" record.
                        // Only insert if <= Today
                        if (d <= DateTime.Today)
                        {
                            // Check if it's a working day
                             bool isHoliday = OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d);
                             bool isWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                             
                             if (!isWeekend && !isHoliday)
                             {
                                 // Create a synthetic Absent record for display
                                 var absentRecord = new AttendanceRecord
                                 {
                                     Id = Guid.Empty, // Dummy ID
                                     EmployeeId = Employee.Id,
                                     Date = d,
                                     Status = AttendanceStatus.Absent,
                                     Branch = Employee.Branch
                                 };
                                 list.Add(new HistoryRecordViewModel(absentRecord, Employee, _holidayService));
                             }
                        }
                    }
                }

                // Add any out-of-range records (e.g. if logic above missed something active)
                // The loop should cover it.
                
                // Sort Descending
                Records = new ObservableCollection<HistoryRecordViewModel>(list.OrderByDescending(x => x.Date));

                // 2. Calculate Stats
                CalculateStats(Records.ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading employee report: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateStats(List<HistoryRecordViewModel> records)
        {
            TotalHours = records.Sum(r => r.HoursWorked);
            TotalOvertime = records.Sum(r => r.OvertimeHours);
            TotalOvertime15 = records.Sum(r => r.OvertimeHours15);
            TotalOvertime20 = records.Sum(r => r.OvertimeHours20);
            
            TotalOvertimePay15 = records.Sum(r => r.OvertimePay15);
            TotalOvertimePay20 = records.Sum(r => r.OvertimePay20);
            
            TotalLates = records.Count(r => r.Attendance.Status == AttendanceStatus.Late);
            TotalPay = records.Sum(r => r.Wage);
            
            TotalNormalPay = TotalPay - (TotalOvertimePay15 + TotalOvertimePay20);
            
            // Absences are now explicit records with Status == Absent
            TotalAbsences = records.Count(r => r.Attendance.Status == AttendanceStatus.Absent);
        }

        [RelayCommand]
        private async Task GenerateReport()
        {
            if (Records == null || !Records.Any()) return;

            var data = Records.OrderByDescending(r => r.Date).Select(r => new
            {
                Date = r.Date.ToShortDateString(),
                In = r.InTime,
                Out = r.OutTime,
                Status = r.Status,
                Hours = r.HoursWorkedDisplay,
                Wage = r.WageDisplay
            });

            // Calculate Normal Hours/Pay (Approximation for Display)
            // Total Hours - Total Overtime Hours
            var normalHours = TotalHours - TotalOvertime;
            // Total Pay - Total Overtime Pay
            var normalPay = TotalPay - (TotalOvertimePay15 + TotalOvertimePay20);

            var title = $"Individual Staff Report: {Employee.DisplayName}";
            
            try 
            {
                // Ensure we use the correct method signature from IExportService
                // Switch to PDF Service
                var summary = new Dictionary<string, string>
                {
                    { "Total Hours", $"{TotalHours:F2}" },
                    { "Normal Hours Pay", $"{normalPay:C}" }, // New Key for PDF Service
                    { "Total Overtime", $"{TotalOvertime:F2}" },
                     // Add breakdown to summary dictionary if PDF service expects it, 
                     // OR rely on PDF service to accept raw data.
                     // The Interface signature expected Dictionary.
                     // Let's add them to dictionary for now.
                    { "Overtime (1.5x)", $"{TotalOvertime15:F2}|{TotalOvertimePay15:C}" },
                    { "Overtime (2.0x)", $"{TotalOvertime20:F2}|{TotalOvertimePay20:C}" },
                    { "Total Lates", $"{TotalLates}" },
                    { "Absences", $"{TotalAbsences}" },
                    { "Gross Pay", $"{TotalPay:C}" }
                };

                var path = await _pdfService.GenerateEmployeeReportPdfAsync(Employee, StartDate, EndDate, data, summary);
                
                // Open PDF
                var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
                p.Start();
            }
            catch(Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Error generating report: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SmartEdit(object? parameter)
        {
            if (parameter is HistoryRecordViewModel record)
            {
                TimeSpan? currentIn = TimeSpan.TryParse(record.InTime, out var inTs) ? inTs : null;
                TimeSpan? currentOut = TimeSpan.TryParse(record.OutTime, out var outTs) ? outTs : null;

                var result = await _dialogService.ShowEditAttendanceAsync(currentIn, currentOut);

                if (result.Confirmed && result.InTime.HasValue)
                {
                    var originalDate = record.Attendance.Date.Date;
                    var newCheckIn = originalDate.Add(result.InTime.Value);
                    
                    DateTime? newCheckOut = null;
                    if (result.OutTime.HasValue)
                    {
                        newCheckOut = originalDate.Add(result.OutTime.Value);
                        if (newCheckOut < newCheckIn) newCheckOut = newCheckOut.Value.AddDays(1);
                    }
                    
                    var now = DateTime.Now;
                    if (newCheckIn > now || (newCheckOut.HasValue && newCheckOut.Value > now))
                    {
                        await _dialogService.ShowAlertAsync("Invalid Time", "Times cannot be in the future.");
                        return;
                    }
                    if (newCheckOut.HasValue && newCheckOut.Value < newCheckIn)
                    {
                        await _dialogService.ShowAlertAsync("Invalid Time", "Clock-out time cannot be before clock-in time.");
                        return;
                    }
                    
                    record.Attendance.CheckInTime = newCheckIn;
                    record.Attendance.CheckOutTime = newCheckOut;
                    
                    if (record.Attendance.Status == AttendanceStatus.Absent)
                    {
                        record.Attendance.Status = AttendanceStatus.Present;
                    }
                    
                    if (record.Attendance.Id != Guid.Empty)
                    {
                        await _timeService.SaveAttendanceRecordAsync(record.Attendance);
                    }
                    else
                    {
                        // It was a dummy record (Absence placeholder), create real record.
                        record.Attendance.Id = Guid.Empty; // Reset just in case
                        await _timeService.SaveAttendanceRecordAsync(record.Attendance);
                    }
                    await LoadData();
                }
            }
        }

        [RelayCommand]
        private async Task DeleteRecord(object? parameter)
        {
            if (parameter is HistoryRecordViewModel record)
            {
                // Dummy absence records can't be deleted because they don't exist in DB
                if (record.Attendance.Id == Guid.Empty) return;

                var confirm = await _dialogService.ShowConfirmationAsync("Delete Record", 
                    $"Are you sure you want to delete the attendance record for {record.EmployeeName} on {record.Date:dd MMM}?");
                    
                if (confirm)
                {
                    await _timeService.DeleteAttendanceRecordAsync(record.Attendance.Id);
                    await LoadData();
                }
            }
        }

        [RelayCommand]
        public async Task UploadSickNote(HistoryRecordViewModel recordVm)
        {
            if (recordVm == null) return;

            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.pdf" };
            var filePath = await _dialogService.PickFileAsync("Select Medical Certificate", extensions);
            
            if (string.IsNullOrEmpty(filePath)) return;

            // Ask how many days the certificate covers
            string? daysInput = await _dialogService.ShowInputAsync("Medical Certificate", "How many days does this medical certificate cover?", "1");
            if (!int.TryParse(daysInput, out int days) || days < 1)
            {
                await _dialogService.ShowAlertAsync("Invalid Input", "Please enter a valid number of days (1 or more).");
                return;
            }

            IsLoading = true;
            try
            {
                // Fetch latest employee record to avoid concurrency conflict and get current balance
                var latestEmployee = await _employeeService.GetEmployeeAsync(Employee.Id);
                if (latestEmployee != null)
                {
                    Employee.RowVersion = latestEmployee.RowVersion ?? Employee.RowVersion;
                    Employee.SickLeaveBalance = latestEmployee.SickLeaveBalance;
                }

                var serverPath = await _timeService.UploadDoctorNoteAsync(filePath);
                if (!string.IsNullOrEmpty(serverPath))
                {
                    var startDate = recordVm.Date.Date;
                    int daysProcessed = 0;
                    int offset = 0;

                    // Support multi-day sick notes
                    while (daysProcessed < days)
                    {
                        var targetDate = startDate.AddDays(offset);
                        offset++;
                        
                        daysProcessed++;

                        bool isHoliday = OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(targetDate);
                        bool isWeekend = targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday;

                        // Only apply to working days
                        if (!isWeekend && !isHoliday)
                        {
                            bool hasSickLeave = Employee.SickLeaveBalance >= 1;
                            var newStatus = hasSickLeave ? AttendanceStatus.Sick : AttendanceStatus.UnpaidSick;

                            if (hasSickLeave)
                            {
                                Employee.SickLeaveBalance--;
                            }

                            // Check if a record already exists
                            var existingRecords = await _timeService.GetDailyAttendanceAsync(targetDate);
                            var employeeRecord = existingRecords.FirstOrDefault(r => r.EmployeeId == Employee.Id);

                            if (employeeRecord != null)
                            {
                                employeeRecord.Status = newStatus;
                                employeeRecord.DoctorsNoteImagePath = serverPath;
                                await _timeService.SaveAttendanceRecordAsync(employeeRecord);
                            }
                            else
                            {
                                // Create a new record since they were absent but no DB record existed to prove it
                                var newRecord = new AttendanceRecord
                                {
                                    EmployeeId = Employee.Id,
                                    Date = targetDate,
                                    Status = newStatus,
                                    Branch = Employee.Branch,
                                    DoctorsNoteImagePath = serverPath
                                };
                                await _timeService.SaveAttendanceRecordAsync(newRecord);
                            }
                        }
                    }

                    // Save the updated sick leave balance to the employee record
                    await _employeeService.UpdateEmployeeAsync(Employee);

                    await _dialogService.ShowAlertAsync("Success", "Medical Certificate uploaded and applied successfully.");
                    await LoadData(); 
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Error", "Failed to upload the Medical Certificate to the server.");
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Upload failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task ViewSickNote(HistoryRecordViewModel recordVm)
        {
            if (recordVm == null || !recordVm.HasSickNote || string.IsNullOrWhiteSpace(recordVm.SickNoteUrl)) return;

            try
            {
                var url = recordVm.SickNoteUrl;
                
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var baseUrl = OCC.Client.Services.Infrastructure.ConnectionSettings.Instance.ApiBaseUrl;
                    if (!baseUrl.EndsWith("/")) baseUrl += "/";
                    url = url.TrimStart('/');
                    url = baseUrl + url;
                }

                var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
                p.Start();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Could not open the Medical Certificate: {ex.Message}");
            }
        }
    }
}
