using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class LoansManagementViewModel : OverlayHostViewModel
    {
        private readonly IEmployeeLoanService _loanService;
        private readonly IEmployeeService _employeeService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<LoansManagementViewModel> _logger;

        [ObservableProperty] private ObservableCollection<EmployeeLoan> _loans = new();
        [ObservableProperty] private EmployeeLoan? _selectedLoan;
        [ObservableProperty] private bool _isAddPanelVisible;

        // AddLoan form fields
        [ObservableProperty] private ObservableCollection<EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private EmployeeSummaryDto? _selectedEmployee;
        [ObservableProperty] private decimal _principalAmount;
        [ObservableProperty] private decimal _monthlyInstallment;
        [ObservableProperty] private decimal _interestRate = 0m;
        [ObservableProperty] private DateTime _loanStartDate = DateTime.Today;
        [ObservableProperty] private string _loanNotes = string.Empty;
        [ObservableProperty] private string _selectedPaymentFrequency = "Fortnightly";
        [ObservableProperty] private int _numberOfInstallments = 10;

        // Computed repayment preview
        public decimal TotalRepayableAmount => MonthlyInstallment * NumberOfInstallments;
        public string RepaymentDurationText => $"{NumberOfInstallments} {SelectedPaymentFrequency}";
 
        partial void OnPrincipalAmountChanged(decimal value)     { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnInterestRateChanged(decimal value)        { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnSelectedPaymentFrequencyChanged(string value) { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnNumberOfInstallmentsChanged(int value)    { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)  { OnPropertyChanged(nameof(RepaymentDurationText)); }

        public LoansManagementViewModel(
            IEmployeeLoanService loanService,
            IEmployeeService employeeService,
            IPdfService pdfService,
            ILogger<LoansManagementViewModel> logger)
        {
            _loanService = loanService;
            _employeeService = employeeService;
            _pdfService = pdfService;
            _logger = logger;
            Title = "Loans Management";
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading loans...";
                var loans = await _loanService.GetAllAsync();
                Loans = new ObservableCollection<EmployeeLoan>(loans.OrderBy(l => l.Employee?.LastName));

                var employees = await _employeeService.GetEmployeesAsync();
                Employees = new ObservableCollection<EmployeeSummaryDto>(
                    employees.OrderBy(e => e.LastName));
                if (Employees.Any()) SelectedEmployee = Employees.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loans");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void ShowAddPanel() => IsAddPanelVisible = true;

        [RelayCommand]
        public void CancelAdd()
        {
            IsAddPanelVisible = false;
            ResetAddForm();
        }

        public override void CloseOverlay()
        {
            CancelAdd();
            base.CloseOverlay();
        }

        [RelayCommand]
        public async Task SaveLoanAsync()
        {
            if (SelectedEmployee == null || PrincipalAmount <= 0 || MonthlyInstallment <= 0) return;

            try
            {
                IsBusy = true;
                BusyText = "Saving loan...";
                var loan = new EmployeeLoan
                {
                    EmployeeId         = SelectedEmployee.Id,
                    PrincipalAmount    = PrincipalAmount,
                    OutstandingBalance = PrincipalAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    IsActive           = true,
                    Notes              = $"[Term: {SelectedPaymentFrequency}, Installments: {NumberOfInstallments}] {LoanNotes}".Trim()
                };
                await _loanService.AddAsync(loan);
                IsAddPanelVisible = false;
                ResetAddForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving loan");
                System.Windows.MessageBox.Show($"Failed to save loan:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task TerminateLoanAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Mark loan for {loan.Employee?.DisplayName} as fully paid / terminated?",
                "Terminate Loan", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                loan.IsActive = false;
                await _loanService.UpdateAsync(loan);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating loan");
                System.Windows.MessageBox.Show($"Failed to update loan:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintLoanScheduleAsync(EmployeeLoan? loan)
        {
            if (loan?.Employee == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating Agreement PDF...";
                var path = await _pdfService.GenerateLoanSchedulePdfAsync(loan, loan.Employee);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan agreement");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintLoanStatementAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating Statement PDF...";
                var statement = await _loanService.GetStatementAsync(loan.Id);
                if (statement == null)
                {
                    System.Windows.MessageBox.Show("Failed to retrieve statement data.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
                var path = await _pdfService.GenerateLoanStatementPdfAsync(statement);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan statement");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void ResetAddForm()
        {
            PrincipalAmount    = 0;
            MonthlyInstallment = 0;
            InterestRate       = 0;
            LoanStartDate      = DateTime.Today;
            LoanNotes          = string.Empty;
            SelectedPaymentFrequency = "Fortnightly";
            NumberOfInstallments = 10;
            if (Employees.Any()) SelectedEmployee = Employees.First();
        }

        private void RecalculateInstallment()
        {
            if (PrincipalAmount <= 0 || NumberOfInstallments <= 0) return;

            if (InterestRate <= 0)
            {
                MonthlyInstallment = Math.Round(PrincipalAmount / NumberOfInstallments, 2);
                return;
            }

            double r = SelectedPaymentFrequency == "Fortnightly"
                ? (double)InterestRate / 100.0 / 26.0
                : (double)InterestRate / 100.0 / 12.0;

            double p = (double)PrincipalAmount;
            double n = NumberOfInstallments;

            double installment = (p * r) / (1 - Math.Pow(1 + r, -n));
            MonthlyInstallment = Math.Round((decimal)installment, 2);
        }
    }
}
