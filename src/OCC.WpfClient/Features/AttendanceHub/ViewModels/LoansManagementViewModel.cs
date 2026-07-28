using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private readonly IDialogService _dialogService;
        private readonly ILogger<LoansManagementViewModel> _logger;

        [ObservableProperty] private ObservableCollection<EmployeeLoan> _loans = new();
        partial void OnLoansChanged(ObservableCollection<EmployeeLoan> value) => OnPropertyChanged(nameof(LoansView));
        [ObservableProperty] private EmployeeLoan? _selectedLoan;
        [ObservableProperty] private bool _isAddPanelVisible;
        [ObservableProperty] private bool _isEditing;

        public IRelayCommand<object>? OpenCommand => EditLoanCommand;
        public IRelayCommand<object>? EditCommand => EditLoanCommand;
        public IRelayCommand<object>? DeleteCommand => DeleteLoanCommand;

        public string PanelHeaderTitle => IsEditing ? "EDIT LOAN DETAILS" : "ADD NEW LOAN";
        public bool IsEmployeeSelectionEnabled => !IsEditing;
        public System.Windows.Visibility SaveAndPrintVisibility => IsEditing ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public System.Windows.Visibility SaveChangesVisibility => IsEditing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        partial void OnIsEditingChanged(bool value)
        {
            OnPropertyChanged(nameof(PanelHeaderTitle));
            OnPropertyChanged(nameof(IsEmployeeSelectionEnabled));
            OnPropertyChanged(nameof(SaveAndPrintVisibility));
            OnPropertyChanged(nameof(SaveChangesVisibility));
        }

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
        public decimal TotalRepayableAmount => PrincipalAmount + (PrincipalAmount * InterestRate / 100);
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
            IDialogService dialogService,
            ILogger<LoansManagementViewModel> logger)
        {
            _loanService = loanService;
            _employeeService = employeeService;
            _pdfService = pdfService;
            _dialogService = dialogService;
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
                
                // Set EndDate for legacy loans if null
                foreach (var loan in loans)
                {
                    if (loan.EndDate == null)
                    {
                        string notes = loan.Notes ?? string.Empty;
                        string freq = "Monthly";
                        int inst = 10;
                        if (notes.Contains("[Term:") && notes.Contains("Installments:"))
                        {
                            int termIndex = notes.IndexOf("[Term: ") + 7;
                            int termEndIndex = notes.IndexOf(",", termIndex);
                            if (termEndIndex > termIndex)
                            {
                                freq = notes.Substring(termIndex, termEndIndex - termIndex).Trim();
                            }
                            int instIndex = notes.IndexOf("Installments: ") + 14;
                            int instEndIndex = notes.IndexOf("]", instIndex);
                            if (instEndIndex > instIndex)
                            {
                                if (int.TryParse(notes.Substring(instIndex, instEndIndex - instIndex), out int instCount))
                                {
                                    inst = instCount;
                                }
                            }
                        }
                        loan.EndDate = CalculateEndDate(loan.StartDate, freq, inst);
                    }
                }

                Loans = new ObservableCollection<EmployeeLoan>(loans.OrderBy(l => l.Employee?.LastName));

                var employees = await _employeeService.GetEmployeesAsync();
                Employees = new ObservableCollection<EmployeeSummaryDto>(
                    employees.Where(e => e.Status == EmployeeStatus.Active).OrderBy(e => e.LastName));
                if (Employees.Any()) SelectedEmployee = Employees.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loans");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void ShowAddPanel()
        {
            IsEditing = false;
            ResetAddForm();
            IsAddPanelVisible = true;
        }

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
                    OutstandingBalance = TotalRepayableAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    EndDate            = CalculateEndDate(LoanStartDate, SelectedPaymentFrequency, NumberOfInstallments),
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
                await _dialogService.ShowAlertAsync("Error", $"Failed to save loan:\n\n{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task SaveAndPrintLoanAsync()
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
                    OutstandingBalance = TotalRepayableAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    EndDate            = CalculateEndDate(LoanStartDate, SelectedPaymentFrequency, NumberOfInstallments),
                    IsActive           = true,
                    Notes              = $"[Term: {SelectedPaymentFrequency}, Installments: {NumberOfInstallments}] {LoanNotes}".Trim()
                };
                
                var savedLoan = await _loanService.AddAsync(loan);
                
                var emp = SelectedEmployee;
                
                IsAddPanelVisible = false;
                ResetAddForm();
                await LoadDataAsync();

                if (savedLoan != null)
                {
                    savedLoan.Employee = new Employee
                    {
                        FirstName = emp.FirstName,
                        LastName = emp.LastName,
                        EmployeeNumber = emp.EmployeeNumber
                    };

                    IsBusy = true;
                    BusyText = "Generating Agreement PDF...";
                    var path = await _pdfService.GenerateLoanSchedulePdfAsync(savedLoan, savedLoan.Employee);
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving and printing loan");
                await _dialogService.ShowAlertAsync("Error", $"Failed to save and print loan:\n\n{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task TerminateLoanAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Terminate Loan",
                $"Mark loan for {loan.Employee?.DisplayName} as fully paid / terminated?");
            if (!confirmed) return;

            try
            {
                loan.IsActive = false;
                await _loanService.UpdateAsync(loan);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating loan");
                await _dialogService.ShowAlertAsync("Error", $"Failed to update loan:\n\n{ex.Message}");
            }
        }

        [RelayCommand]
        public void EditLoan(object? parameter)
        {
            var loan = parameter as EmployeeLoan ?? SelectedLoan;
            if (loan == null) return;
            SelectedLoan = loan;
            IsEditing = true;

            // Populate form fields
            SelectedEmployee = Employees.FirstOrDefault(e => e.Id == loan.EmployeeId);
            PrincipalAmount = loan.PrincipalAmount;
            InterestRate = loan.InterestRate;
            LoanStartDate = loan.StartDate;
            
            // Parse notes
            string notes = loan.Notes ?? string.Empty;
            if (notes.StartsWith("[Term:") && notes.Contains("Installments:"))
            {
                int termIndex = notes.IndexOf("[Term: ") + 7;
                int termEndIndex = notes.IndexOf(",", termIndex);
                if (termEndIndex > termIndex)
                {
                    SelectedPaymentFrequency = notes.Substring(termIndex, termEndIndex - termIndex).Trim();
                }
                int instIndex = notes.IndexOf("Installments: ") + 14;
                int instEndIndex = notes.IndexOf("]", instIndex);
                if (instEndIndex > instIndex)
                {
                    if (int.TryParse(notes.Substring(instIndex, instEndIndex - instIndex), out int instCount))
                    {
                        NumberOfInstallments = instCount;
                    }
                }
                int closingBracket = notes.IndexOf("]");
                LoanNotes = closingBracket >= 0 && closingBracket + 1 < notes.Length ? notes.Substring(closingBracket + 1).Trim() : string.Empty;
            }
            else
            {
                SelectedPaymentFrequency = "Monthly";
                NumberOfInstallments = 10;
                LoanNotes = notes;
            }

            IsAddPanelVisible = true;
        }

        [RelayCommand]
        public async Task SaveEditLoanAsync()
        {
            if (SelectedLoan == null || SelectedEmployee == null || PrincipalAmount <= 0 || MonthlyInstallment <= 0) return;

            try
            {
                IsBusy = true;
                BusyText = "Saving loan changes...";
                var loan = SelectedLoan;
                loan.EmployeeId = SelectedEmployee.Id;
                loan.PrincipalAmount = PrincipalAmount;
                loan.MonthlyInstallment = MonthlyInstallment;
                loan.InterestRate = InterestRate;
                loan.StartDate = LoanStartDate;
                
                // Get statement/payments to see how much was already paid
                decimal paidSoFar = 0;
                try
                {
                    var statement = await _loanService.GetStatementAsync(loan.Id);
                    if (statement != null)
                    {
                        paidSoFar = statement.Payments.Sum(p => p.Amount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get loan statement to calculate payments made so far.");
                }

                loan.OutstandingBalance = TotalRepayableAmount - paidSoFar;
                if (loan.OutstandingBalance <= 0)
                {
                    loan.OutstandingBalance = 0;
                    loan.IsActive = false;
                    loan.EndDate = DateTime.Today;
                }
                else
                {
                    loan.EndDate = CalculateEndDate(LoanStartDate, SelectedPaymentFrequency, NumberOfInstallments);
                }

                // Build notes
                loan.Notes = $"[Term: {SelectedPaymentFrequency}, Installments: {NumberOfInstallments}] {LoanNotes}".Trim();

                await _loanService.UpdateAsync(loan);
                IsAddPanelVisible = false;
                ResetAddForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving loan changes");
                await _dialogService.ShowAlertAsync("Error", $"Failed to save loan changes:\n\n{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task DeleteLoanAsync(object? parameter)
        {
            var targets = new List<EmployeeLoan>();
            if (parameter is System.Collections.IList list)
            {
                foreach (var item in list)
                {
                    if (item is EmployeeLoan typedItem)
                    {
                        targets.Add(typedItem);
                    }
                }
            }
            else if (parameter is EmployeeLoan item)
            {
                targets.Add(item);
            }
            else if (SelectedLoan != null)
            {
                targets.Add(SelectedLoan);
            }

            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Loans" : "Delete Loan";
            
            string message;
            bool hasActiveLoans = targets.Any(l => l.IsActive && l.OutstandingBalance > 0);
            
            if (targets.Count > 1)
            {
                if (hasActiveLoans)
                {
                    message = $"You are about to delete {targets.Count} loans. Some of these loans are currently ACTIVE. Deleting them will permanently erase the loan records and all payment histories.\n\n" +
                              "If employees have made payments, you should use the 'Receive Manual Payment' option instead of deleting the loan.\n\n" +
                              "Are you sure you want to proceed with deleting these loans anyway?";
                }
                else
                {
                    message = $"You are about to delete {targets.Count} loans. This action cannot be undone. Are you sure you want to proceed?";
                }
            }
            else
            {
                var target = targets[0];
                var empName = target.Employee?.DisplayName ?? "the employee";
                if (target.IsActive && target.OutstandingBalance > 0)
                {
                    message = $"The loan for {empName} is currently ACTIVE with an outstanding balance of R {target.OutstandingBalance:F2}.\n\n" +
                              "Deleting this loan will permanently erase the loan record and all history. If the employee wants to pay off the loan, you should use the 'Receive Manual Payment' option instead.\n\n" +
                              "Are you sure you want to permanently delete this loan?";
                }
                else
                {
                    message = $"Are you sure you want to permanently delete the loan for {empName}? This action cannot be undone.";
                }
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting loans..." : "Deleting loan...";
                foreach (var target in targets)
                {
                    await _loanService.DeleteAsync(target.Id);
                }
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting loan(s)");
                await _dialogService.ShowAlertAsync("Error", $"Failed to delete loan:\n\n{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task ReceiveManualPaymentAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            if (!loan.IsActive)
            {
                await _dialogService.ShowAlertAsync("Manual Payment", "This loan is already paid off.");
                return;
            }

            var input = await _dialogService.ShowInputDialogAsync(
                "Receive Manual Payment", 
                $"Enter the manual payment amount for {loan.Employee?.DisplayName} (Outstanding: R {loan.OutstandingBalance:F2}):",
                "0.00");

            if (string.IsNullOrWhiteSpace(input)) return;

            if (decimal.TryParse(input, out decimal paymentAmount) && paymentAmount > 0)
            {
                if (paymentAmount > loan.OutstandingBalance)
                {
                    await _dialogService.ShowAlertAsync("Invalid Amount", $"The payment amount cannot exceed the outstanding balance of R {loan.OutstandingBalance:F2}.");
                    return;
                }

                try
                {
                    IsBusy = true;
                    BusyText = "Processing payment...";
                    
                    loan.OutstandingBalance -= paymentAmount;
                    if (loan.OutstandingBalance <= 0)
                    {
                        loan.OutstandingBalance = 0;
                        loan.IsActive = false;
                        loan.EndDate = DateTime.Today;
                    }

                    // Log the manual payment in the notes to keep a history
                    loan.Notes = $"{loan.Notes}\n[Manual Payment: R {paymentAmount:F2} on {DateTime.Today:dd MMM yyyy}]".Trim();

                    await _loanService.UpdateAsync(loan);
                    
                    await _dialogService.ShowAlertAsync("Payment Received", $"Successfully received payment of R {paymentAmount:F2} for {loan.Employee?.DisplayName}.");
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing manual payment");
                    await _dialogService.ShowAlertAsync("Error", $"Failed to process payment:\n\n{ex.Message}");
                }
                finally { IsBusy = false; }
            }
            else
            {
                await _dialogService.ShowAlertAsync("Invalid Amount", "Please enter a valid numeric amount greater than zero.");
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
                await _dialogService.ShowAlertAsync("Error", $"Failed to generate PDF:\n\n{ex.Message}");
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
                    await _dialogService.ShowAlertAsync("Error", "Failed to retrieve statement data.");
                    return;
                }
                var path = await _pdfService.GenerateLoanStatementPdfAsync(statement);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan statement");
                await _dialogService.ShowAlertAsync("Error", $"Failed to generate PDF:\n\n{ex.Message}");
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

            decimal totalRepayable = PrincipalAmount + (PrincipalAmount * InterestRate / 100);
            MonthlyInstallment = Math.Round(totalRepayable / NumberOfInstallments, 2);
        }

        // ─── Search and Filtering ──────────────────────────────────────────

        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _branchFilter = "All";
        [ObservableProperty] private string _statusFilter = "All";
        [ObservableProperty] private bool _isAdvancedFiltersVisible;
        
        [ObservableProperty] private decimal? _minPrincipal;
        [ObservableProperty] private decimal? _maxPrincipal;
        [ObservableProperty] private decimal? _minInstallment;
        [ObservableProperty] private decimal? _maxInstallment;
        [ObservableProperty] private decimal? _minBalance;
        [ObservableProperty] private decimal? _maxBalance;
        [ObservableProperty] private decimal? _minInterestRate;
        [ObservableProperty] private decimal? _maxInterestRate;
        [ObservableProperty] private DateTime? _startDateFrom;
        [ObservableProperty] private DateTime? _startDateTo;

        public ICollectionView LoansView
        {
            get
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Loans);
                view.Filter = FilterLoans;
                return view;
            }
        }

        partial void OnSearchQueryChanged(string value) => LoansView.Refresh();
        partial void OnBranchFilterChanged(string value) => LoansView.Refresh();
        partial void OnStatusFilterChanged(string value) => LoansView.Refresh();
        partial void OnMinPrincipalChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxPrincipalChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinInstallmentChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxInstallmentChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinBalanceChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxBalanceChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinInterestRateChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxInterestRateChanged(decimal? value) => LoansView.Refresh();
        partial void OnStartDateFromChanged(DateTime? value) => LoansView.Refresh();
        partial void OnStartDateToChanged(DateTime? value) => LoansView.Refresh();

        private bool FilterLoans(object obj)
        {
            if (obj is not EmployeeLoan loan) return false;

            // Search query (Employee Name)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var name = loan.Employee?.DisplayName ?? string.Empty;
                if (!name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Branch filter
            if (BranchFilter != "All" && !string.IsNullOrWhiteSpace(BranchFilter))
            {
                var branch = loan.Employee?.Branch.ToString() ?? string.Empty;
                if (!branch.Equals(BranchFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Status filter
            if (StatusFilter != "All" && !string.IsNullOrWhiteSpace(StatusFilter))
            {
                bool expectedActive = StatusFilter.Equals("Active", StringComparison.OrdinalIgnoreCase);
                if (loan.IsActive != expectedActive)
                    return false;
            }

            // Principal
            if (MinPrincipal.HasValue && loan.PrincipalAmount < MinPrincipal.Value) return false;
            if (MaxPrincipal.HasValue && loan.PrincipalAmount > MaxPrincipal.Value) return false;

            // Balance
            if (MinBalance.HasValue && loan.OutstandingBalance < MinBalance.Value) return false;
            if (MaxBalance.HasValue && loan.OutstandingBalance > MaxBalance.Value) return false;

            // Installment
            if (MinInstallment.HasValue && loan.MonthlyInstallment < MinInstallment.Value) return false;
            if (MaxInstallment.HasValue && loan.MonthlyInstallment > MaxInstallment.Value) return false;

            // Interest Rate
            if (MinInterestRate.HasValue)
            {
                decimal rateVal = loan.InterestRate;
                if (rateVal < 1m && MinInterestRate.Value > 1m) rateVal = rateVal * 100m;
                if (rateVal < MinInterestRate.Value) return false;
            }
            if (MaxInterestRate.HasValue)
            {
                decimal rateVal = loan.InterestRate;
                if (rateVal < 1m && MaxInterestRate.Value > 1m) rateVal = rateVal * 100m;
                if (rateVal > MaxInterestRate.Value) return false;
            }

            // Start Date
            if (StartDateFrom.HasValue && loan.StartDate < StartDateFrom.Value) return false;
            if (StartDateTo.HasValue && loan.StartDate > StartDateTo.Value) return false;

            return true;
        }

        [RelayCommand]
        public void ToggleAdvancedFilters() => IsAdvancedFiltersVisible = !IsAdvancedFiltersVisible;

        [RelayCommand]
        public void ClearFilters()
        {
            SearchQuery = string.Empty;
            BranchFilter = "All";
            StatusFilter = "All";
            MinPrincipal = null;
            MaxPrincipal = null;
            MinInstallment = null;
            MaxInstallment = null;
            MinBalance = null;
            MaxBalance = null;
            MinInterestRate = null;
            MaxInterestRate = null;
            StartDateFrom = null;
            StartDateTo = null;
            LoansView.Refresh();
        }

        [RelayCommand]
        public async Task PrintFilteredLoansAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating Report PDF...";
                
                var filteredItems = LoansView.Cast<EmployeeLoan>().ToList();
                
                var cols = new List<ReportColumnDefinition>
                {
                    new() { Header = "Employee", PropertyName = "Employee", Width = 2.0 },
                    new() { Header = "Branch", PropertyName = "Branch", Width = 1.0 },
                    new() { Header = "Principal (R)", PropertyName = "Principal", Width = 1.0 },
                    new() { Header = "Installment (R)", PropertyName = "Installment", Width = 1.0 },
                    new() { Header = "Balance (R)", PropertyName = "Balance", Width = 1.0 },
                    new() { Header = "Admin Fee (%)", PropertyName = "InterestPercent", Width = 0.8 },
                    new() { Header = "Duration", PropertyName = "Duration", Width = 1.2 },
                    new() { Header = "Start Date", PropertyName = "StartDate", Width = 1.2 },
                    new() { Header = "Finish Date", PropertyName = "FinishDate", Width = 1.2 },
                    new() { Header = "Status", PropertyName = "Status", Width = 0.8 }
                };

                var printItems = filteredItems.Select(l => {
                    string notes = l.Notes ?? string.Empty;
                    string duration = "10 Monthly";
                    if (notes.Contains("[Term:") && notes.Contains("Installments:"))
                    {
                        int termIndex = notes.IndexOf("[Term: ") + 7;
                        int termEndIndex = notes.IndexOf(",", termIndex);
                        string freq = "Monthly";
                        if (termEndIndex > termIndex)
                        {
                            freq = notes.Substring(termIndex, termEndIndex - termIndex).Trim();
                        }
                        int instIndex = notes.IndexOf("Installments: ") + 14;
                        int instEndIndex = notes.IndexOf("]", instIndex);
                        int inst = 10;
                        if (instEndIndex > instIndex)
                        {
                            if (int.TryParse(notes.Substring(instIndex, instEndIndex - instIndex), out int instCount))
                            {
                                inst = instCount;
                            }
                        }
                        duration = $"{inst} {freq}";
                    }

                    var finishDate = l.EndDate ?? CalculateEndDate(l.StartDate, notes.Contains("Fortnightly") ? "Fortnightly" : "Monthly", 10);

                    return new LoanPrintModel
                    {
                        Employee = l.Employee?.DisplayName ?? "Unknown",
                        Branch = l.Employee?.Branch.ToString() ?? "Unknown",
                        Principal = l.PrincipalAmount,
                        Installment = l.MonthlyInstallment,
                        Balance = l.OutstandingBalance,
                        InterestPercent = (double)(l.InterestRate < 1m && l.InterestRate > 0m ? l.InterestRate * 100m : l.InterestRate),
                        StartDate = l.StartDate,
                        Duration = duration,
                        FinishDate = finishDate,
                        Status = l.IsActive ? "ACTIVE" : "PAID"
                    };
                }).ToList();

                var path = await _pdfService.GenerateListReportPdfAsync("Staff Loans Report", printItems, cols, isLandscape: true);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing filtered loans");
                await _dialogService.ShowAlertAsync("Error", $"Failed to generate PDF:\n\n{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private DateTime CalculateEndDate(DateTime startDate, string frequency, int installments)
        {
            if (installments <= 0) return startDate;
            if (frequency == "Fortnightly")
            {
                return startDate.AddDays(installments * 14);
            }
            else // Monthly
            {
                return startDate.AddMonths(installments);
            }
        }
    }

    public class LoanPrintModel
    {
        public string Employee { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal Installment { get; set; }
        public decimal Balance { get; set; }
        public double InterestPercent { get; set; }
        public DateTime StartDate { get; set; }
        public string Duration { get; set; } = string.Empty;
        public DateTime FinishDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
