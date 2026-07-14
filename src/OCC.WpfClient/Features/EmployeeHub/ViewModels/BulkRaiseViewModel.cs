using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.EmployeeHub.ViewModels
{
    public partial class BulkRaiseEmployeePreview : ObservableObject
    {
        public Guid Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double OldRate { get; set; }

        [ObservableProperty]
        private double _newRate;

        [ObservableProperty]
        private bool _isChecked = true;
    }

    public partial class BulkRaiseViewModel : DetailViewModelBase
    {
        private readonly IEmployeeService _employeeService;

        [ObservableProperty]
        private double _increasePercentage = 6.0;

        [ObservableProperty]
        private ObservableCollection<BulkRaiseEmployeePreview> _previews = new();

        public BulkRaiseViewModel(
            IEmployeeService employeeService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _employeeService = employeeService;
            Title = "Bulk Wage Increase";
            _ = LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading active hourly employees...";
                var summaries = await _employeeService.GetEmployeesAsync();
                
                // We filter for active hourly employees
                var hourlyEmployees = summaries
                    .Where(e => e.Status == EmployeeStatus.Active && e.RateType == RateType.Hourly)
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .ToList();

                var list = new List<BulkRaiseEmployeePreview>();
                foreach (var emp in hourlyEmployees)
                {
                    list.Add(new BulkRaiseEmployeePreview
                    {
                        Id = emp.Id,
                        EmployeeNumber = emp.EmployeeNumber,
                        Name = $"{emp.FirstName} {emp.LastName}",
                        OldRate = emp.HourlyRate,
                        NewRate = Math.Round(emp.HourlyRate * (1 + IncreasePercentage / 100.0), 2)
                    });
                }

                Previews = new ObservableCollection<BulkRaiseEmployeePreview>(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employees for bulk raise");
                NotifyError("Error", "Failed to load active hourly employees.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnIncreasePercentageChanged(double value)
        {
            RecalculateNewRates();
        }

        private void RecalculateNewRates()
        {
            foreach (var item in Previews)
            {
                item.NewRate = Math.Round(item.OldRate * (1 + IncreasePercentage / 100.0), 2);
            }
        }

        protected override async Task ExecuteSaveAsync()
        {
            var selected = Previews.Where(p => p.IsChecked).ToList();
            if (!selected.Any())
            {
                NotifyError("No selection", "Please select at least one employee to apply the raise.");
                throw new Exception("No employees selected");
            }

            int successCount = 0;
            int total = selected.Count;

            foreach (var item in selected)
            {
                try
                {
                    BusyText = $"Updating {item.Name} ({successCount + 1}/{total})...";
                    var dto = await _employeeService.GetEmployeeAsync(item.Id);
                    if (dto != null)
                    {
                        var employee = new Employee
                        {
                            Id = dto.Id,
                            LinkedUserId = dto.LinkedUserId,
                            FirstName = dto.FirstName,
                            LastName = dto.LastName,
                            EmployeeNumber = dto.EmployeeNumber,
                            IdNumber = dto.IdNumber,
                            IdType = dto.IdType,
                            PermitNumber = dto.PermitNumber,
                            Email = dto.Email,
                            Phone = dto.Phone,
                            PhysicalAddress = dto.PhysicalAddress,
                            DoB = dto.DoB,
                            Role = dto.Role,
                            Status = dto.Status,
                            EmploymentType = dto.EmploymentType,
                            ContractDuration = dto.ContractDuration,
                            EmploymentDate = dto.EmploymentDate,
                            Branch = dto.Branch,
                            LivesInCompanyHousing = dto.LivesInCompanyHousing,
                            ShiftStartTime = dto.ShiftStartTime,
                            ShiftEndTime = dto.ShiftEndTime,
                            RateType = dto.RateType,
                            HourlyRate = item.NewRate, // Apply new rate
                            TaxNumber = dto.TaxNumber,
                            BankName = dto.BankName,
                            AccountNumber = dto.AccountNumber,
                            BranchCode = dto.BranchCode,
                            AccountType = dto.AccountType,
                            AnnualLeaveBalance = dto.AnnualLeaveBalance,
                            SickLeaveBalance = dto.SickLeaveBalance,
                            LeaveCycleStartDate = dto.LeaveCycleStartDate,
                            NextOfKinName = dto.NextOfKinName,
                            NextOfKinRelation = dto.NextOfKinRelation,
                            NextOfKinPhone = dto.NextOfKinPhone,
                            EmergencyContactName = dto.EmergencyContactName,
                            EmergencyContactPhone = dto.EmergencyContactPhone,
                            RowVersion = dto.RowVersion
                        };

                        var success = await _employeeService.UpdateEmployeeAsync(employee);
                        if (success)
                        {
                            successCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating rate for employee {Id}", item.Id);
                }
            }

            if (successCount > 0)
            {
                NotifySuccess("Success", $"Successfully updated rates for {successCount} employees.");
            }
            else
            {
                throw new Exception("Failed to update any employee rates.");
            }
        }

        protected override Task ExecuteReloadAsync() => LoadEmployeesAsync();

        protected override string GetReportTitle() => "Bulk Raise Preview Report";

        protected override object GetReportItem() => Previews.Where(p => p.IsChecked).Select(p => new
        {
            p.EmployeeNumber,
            p.Name,
            OldRate = $"R {p.OldRate:F2}",
            NewRate = $"R {p.NewRate:F2}",
            Increase = $"{IncreasePercentage}%"
        }).ToList();

        public override async Task PrintAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating report...";
                
                if (_pdfService == null)
                {
                    _logger?.LogError("IPdfService is not initialized. Ensure it is registered in the DI container.");
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                var selected = Previews.Where(p => p.IsChecked).Select(p => new
                {
                    p.EmployeeNumber,
                    p.Name,
                    OldRate = $"R {p.OldRate:F2}",
                    NewRate = $"R {p.NewRate:F2}",
                    Increase = $"{IncreasePercentage}%"
                }).ToList();

                var cols = new List<ReportColumnDefinition>
                {
                    new() { Header = "Emp #", PropertyName = "EmployeeNumber", Width = 1.0 },
                    new() { Header = "Employee Name", PropertyName = "Name", Width = 2.5 },
                    new() { Header = "Old Rate", PropertyName = "OldRate", Width = 1.5 },
                    new() { Header = "New Rate", PropertyName = "NewRate", Width = 1.5 },
                    new() { Header = "Increase", PropertyName = "Increase", Width = 1.2 }
                };

                var path = await _pdfService.GenerateListReportPdfAsync("Bulk Raise Preview Report", selected, cols, false);
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing bulk raise report");
                NotifyError("Print Error", "An error occurred while generating the PDF report.");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
