using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public class TempImportRow
    {
        public string RawEmployeeName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string RawSiteName { get; set; } = string.Empty;
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
    }

    public partial class AttendanceImportPreviewViewModel : OverlayViewModel
    {
        private readonly IAttendanceService _attendanceService;

        [ObservableProperty] private ObservableCollection<AttendanceImportRow> _rows = new();
        [ObservableProperty] private List<EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private List<ProjectSummaryDto> _projects = new();

        public AttendanceImportPreviewViewModel(
            List<TempImportRow> parsedRows,
            List<EmployeeSummaryDto> employees,
            List<ProjectSummaryDto> projects,
            IAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
            Employees = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();
            
            // Build projects list with "Other"
            var pList = new List<ProjectSummaryDto>
            {
                new ProjectSummaryDto { Id = Guid.Empty, Name = "Other (Specify)..." }
            };
            pList.AddRange(projects.OrderBy(p => p.Name));
            Projects = pList;

            Title = "PREVIEW IMPORTED ATTENDANCE DATA";

            // Map the parsed rows
            foreach (var parsed in parsedRows)
            {
                var row = new AttendanceImportRow
                {
                    RawEmployeeName = parsed.RawEmployeeName,
                    RawSiteName = parsed.RawSiteName,
                    Date = parsed.Date,
                    CheckInTime = parsed.CheckInTime,
                    CheckOutTime = parsed.CheckOutTime
                };

                row.CheckExistingCallback = CheckExistingRecord;

                // Match employee
                var matchedEmp = FindEmployeeMatch(parsed.RawEmployeeName);
                row.SelectedEmployee = matchedEmp;

                // Match project/site
                if (IsAbsentStatus(parsed.RawSiteName))
                {
                    row.Status = AttendanceStatus.Absent;
                    row.SelectedProject = null;
                }
                else if (IsSickStatus(parsed.RawSiteName))
                {
                    row.Status = AttendanceStatus.Sick;
                    row.SelectedProject = null;
                }
                else
                {
                    row.Status = AttendanceStatus.Present;
                    var matchedProj = FindProjectMatch(parsed.RawSiteName);
                    if (matchedProj != null)
                    {
                        row.SelectedProject = matchedProj;
                    }
                    else
                    {
                        row.SelectedProject = Projects.First(p => p.Id == Guid.Empty); // Other
                        row.CustomSite = parsed.RawSiteName;
                    }
                }

                row.PropertyChanged += Row_PropertyChanged;
                row.Validate();
                Rows.Add(row);
            }

            Rows.CollectionChanged += Rows_CollectionChanged;
            _ = InitializeAsync();
        }

        private readonly List<AttendanceRecord> _existingRecords = new();

        public bool CheckExistingRecord(Guid employeeId, DateTime date)
        {
            return _existingRecords.Any(r => r.EmployeeId == employeeId && r.Date.Date == date.Date);
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Checking for existing records...";

                if (Rows.Count > 0)
                {
                    var minDate = Rows.Min(r => r.Date);
                    var maxDate = Rows.Max(r => r.Date);

                    var records = await _attendanceService.GetAttendanceRecordsAsync(minDate, maxDate);
                    if (records != null)
                    {
                        _existingRecords.Clear();
                        _existingRecords.AddRange(records);
                    }
                }

                // Re-validate all rows now that existing records are loaded
                foreach (var row in Rows)
                {
                    row.Validate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking existing records: {ex}");
            }
            finally
            {
                IsBusy = false;
                SaveImportCommand.NotifyCanExecuteChanged();
            }
        }

        private EmployeeSummaryDto? FindEmployeeMatch(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return null;

            var normalized = rawName.Replace("  ", " ").Trim();

            // 1. Direct match by contains (checking if our employee's full name is in the string, or vice versa)
            foreach (var emp in Employees)
            {
                var fullName = $"{emp.FirstName} {emp.LastName}";
                if (string.Equals(fullName, normalized, StringComparison.OrdinalIgnoreCase))
                    return emp;
            }

            // 2. Fuzzy match: Check if rawName contains both FirstName and LastName of the employee
            foreach (var emp in Employees)
            {
                if (!string.IsNullOrEmpty(emp.FirstName) && !string.IsNullOrEmpty(emp.LastName))
                {
                    if (normalized.Contains(emp.FirstName, StringComparison.OrdinalIgnoreCase) &&
                        normalized.Contains(emp.LastName, StringComparison.OrdinalIgnoreCase))
                    {
                        return emp;
                    }
                }
            }

            return null;
        }

        private ProjectSummaryDto? FindProjectMatch(string siteName)
        {
            if (string.IsNullOrWhiteSpace(siteName)) return null;

            // Direct or partial match on name
            return Projects.FirstOrDefault(p => 
                p.Id != Guid.Empty && 
                (p.Name.Contains(siteName, StringComparison.OrdinalIgnoreCase) || 
                 siteName.Contains(p.Name, StringComparison.OrdinalIgnoreCase)));
        }

        private bool IsAbsentStatus(string site)
        {
            return site.Equals("ABSENT", StringComparison.OrdinalIgnoreCase) ||
                   site.Equals("ABSCONDED", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSickStatus(string site)
        {
            return site.Equals("SICK", StringComparison.OrdinalIgnoreCase) ||
                   site.Contains("CLINIC", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanSaveImport()
        {
            return Rows != null && Rows.Count > 0 && Rows.All(r => r.IsValid);
        }

        private void Rows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (System.ComponentModel.INotifyPropertyChanged oldRow in e.OldItems)
                {
                    oldRow.PropertyChanged -= Row_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (System.ComponentModel.INotifyPropertyChanged newRow in e.NewItems)
                {
                    newRow.PropertyChanged += Row_PropertyChanged;
                }
            }
            SaveImportCommand.NotifyCanExecuteChanged();
        }

        private void Row_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AttendanceImportRow.IsValid))
            {
                SaveImportCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveImport))]
        private async Task SaveImportAsync()
        {
            var validRows = Rows.Where(r => r.IsValid).ToList();
            if (validRows.Count == 0)
            {
                NotifyWarning("Cannot Save", "There are no valid rows to import. Please resolve validation issues (e.g. match employees).");
                return;
            }

            IsBusy = true;
            BusyText = $"Saving {validRows.Count} records...";

            int successCount = 0;
            try
            {
                foreach (var row in validRows)
                {
                    // Enforce skip existing to make absolutely sure we do not overwrite/duplicate
                    if (CheckExistingRecord(row.SelectedEmployee!.Id, row.Date))
                    {
                        continue;
                    }

                    var record = new AttendanceRecord
                    {
                        EmployeeId = row.SelectedEmployee!.Id,
                        Date = row.Date,
                        Status = row.Status,
                        Branch = row.SelectedEmployee.Branch ?? string.Empty
                    };

                    if (row.Status == AttendanceStatus.Present || row.Status == AttendanceStatus.Late || row.Status == AttendanceStatus.LeaveEarly)
                    {
                        if (row.SelectedProject?.Id == Guid.Empty) // Other
                        {
                            record.ProjectId = null;
                            record.CustomSite = row.CustomSite;
                        }
                        else
                        {
                            record.ProjectId = row.SelectedProject?.Id;
                            record.CustomSite = null;
                        }

                        if (row.CheckInTime.HasValue)
                        {
                            record.CheckInTime = row.Date.Date.Add(row.CheckInTime.Value);
                        }
                        if (row.CheckOutTime.HasValue)
                        {
                            record.CheckOutTime = row.Date.Date.Add(row.CheckOutTime.Value);
                        }
                    }
                    else
                    {
                        record.ProjectId = null;
                        record.CustomSite = null;
                        record.Notes = row.RawSiteName; // Save original status notes like "ABSENT -CLINIC"
                    }

                    await _attendanceService.CreateAttendanceRecordAsync(record);
                    successCount++;
                }

                Close(successCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during bulk attendance import: {ex}");
                NotifyWarning("Import Warning", $"Imported {successCount} records, but encountered an error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
