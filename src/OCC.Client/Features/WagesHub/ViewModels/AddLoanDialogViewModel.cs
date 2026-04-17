using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class AddLoanDialogViewModel : ObservableObject
    {
        private readonly IEmployeeService _employeeService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;
        
        public Action<EmployeeLoan?>? CloseAction { get; set; }

        public AddLoanDialogViewModel(
            IEmployeeService employeeService,
            ISettingsService settingsService,
            IDialogService dialogService,
            IPdfService pdfService)
        {
            _employeeService = employeeService;
            _settingsService = settingsService;
            _dialogService = dialogService;
            _pdfService = pdfService;
            
            StartDate = DateTime.Today;
            LoadData();
        }

        private async void LoadData()
        {
            await LoadEmployees();
            await LoadSettings();
        }

        private async Task LoadSettings()
        {
            try
            {
                var companySettings = await _settingsService.GetCompanyDetailsAsync();
                InterestRate = companySettings.GlobalLoanInterestRate;
            }
            catch (Exception)
            {
                InterestRate = 0; 
            }
        }

        #region Observables

        [ObservableProperty]
        private ObservableCollection<EmployeeSummaryDto> _employees = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepaymentDurationText))]
        [NotifyPropertyChangedFor(nameof(TotalRepayableAmount))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private EmployeeSummaryDto? _selectedEmployee;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepaymentDurationText))]
        [NotifyPropertyChangedFor(nameof(TotalRepayableAmount))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private decimal _principalAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepaymentDurationText))]
        [NotifyPropertyChangedFor(nameof(TotalRepayableAmount))]
        private decimal _monthlyInstallment;

        [ObservableProperty]
        private DateTime _startDate;
        
        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepaymentDurationText))]
        [NotifyPropertyChangedFor(nameof(TotalRepayableAmount))]
        private decimal _interestRate;

        #endregion

        public decimal TotalRepayableAmount => CalculateTotalRepayable();

        public string RepaymentDurationText
        {
            get
            {
                if (PrincipalAmount <= 0 || MonthlyInstallment <= 0) return "-";
                
                var totalAmount = TotalRepayableAmount;
                if (totalAmount == 0) return "Indefinite (Installment too low)";

                // Number of payments
                var numberOfPayments = totalAmount / MonthlyInstallment;
                var payments = (double)numberOfPayments;

                if (SelectedEmployee?.RateType == RateType.Hourly)
                {
                    // For hourly employees, installments are usually per fortnight (South African context for this client)
                    return $"{payments:N1} Fortnights";
                }
                else
                {
                    // Monthly
                    return $"{payments:N1} Months";
                }
            }
        }

        private decimal CalculateTotalRepayable()
        {
            if (MonthlyInstallment <= 0 || PrincipalAmount <= 0) return 0;
            if (InterestRate <= 0) return PrincipalAmount;

            // Simple interest or Amortization? The UI seems to favor an amortization approach based on old code
            double rate = (double)InterestRate / 100.0;
            // Monthly rate for Monthly employees, Fortnightly rate for Hourly?
            // Regulation usually uses monthly reference.
            double periodicRate = rate / 12.0; 
            
            double p = (double)PrincipalAmount;
            double i = (double)MonthlyInstallment;

            if (i <= p * periodicRate) return 0; // Infinite

            double n = -Math.Log(1 - (periodicRate * p) / i) / Math.Log(1 + periodicRate);
            return (decimal)(n * i);
        }

        private async Task LoadEmployees()
        {
            var employees = await _employeeService.GetEmployeesAsync();
            Employees = new ObservableCollection<EmployeeSummaryDto>(employees.OrderBy(e => e.FirstName));
            if (Employees.Any()) SelectedEmployee = Employees.First();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            if (SelectedEmployee == null) return;

            var loan = new EmployeeLoan
            {
                EmployeeId = SelectedEmployee.Id,
                PrincipalAmount = PrincipalAmount,
                OutstandingBalance = PrincipalAmount, 
                MonthlyInstallment = MonthlyInstallment,
                StartDate = StartDate,
                IsActive = true,
                InterestRate = InterestRate,
                Notes = Notes
            };
            
            CloseAction?.Invoke(loan);
        }

        private bool CanSave() => SelectedEmployee != null && PrincipalAmount > 0 && MonthlyInstallment > 0;

        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke(null);
        }

        [RelayCommand]
        private async Task Print()
        {
             if (SelectedEmployee == null) return;

             try 
             {
                 IsBusy = true;
                 
                 var tempLoan = new EmployeeLoan
                 {
                     EmployeeId = SelectedEmployee.Id,
                     PrincipalAmount = PrincipalAmount,
                     MonthlyInstallment = MonthlyInstallment,
                     StartDate = StartDate,
                     InterestRate = InterestRate
                 };

                 var fullEmployeeDto = await _employeeService.GetEmployeeAsync(SelectedEmployee.Id); 
                 if (fullEmployeeDto == null) throw new Exception("Employee data not found.");
                 
                 // Map DTO to Model for QuestPDF service
                 var empModel = new Employee
                 {
                     FirstName = fullEmployeeDto.FirstName,
                     LastName = fullEmployeeDto.LastName,
                     IdNumber = fullEmployeeDto.IdNumber,
                     EmployeeNumber = fullEmployeeDto.EmployeeNumber,
                     Branch = fullEmployeeDto.Branch,
                     Phone = fullEmployeeDto.Phone,
                     Email = fullEmployeeDto.Email,
                     PhysicalAddress = fullEmployeeDto.PhysicalAddress
                 };

                 var path = await _pdfService.GenerateLoanSchedulePdfAsync(tempLoan, empModel);
                 
                 System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                 {
                     FileName = path,
                     UseShellExecute = true
                 });
             }
             catch(Exception ex)
             {
                 await _dialogService.ShowAlertAsync("Print Error", $"Failed to generate PDF: {ex.Message}");
             }
             finally
             {
                 IsBusy = false;
             }
        }

        [ObservableProperty]
        private bool _isBusy;

        [RelayCommand]
        private async Task ReportBug()
        {
            await _dialogService.ShowBugReportAsync("AddLoanDialog");
        }
    }
}
