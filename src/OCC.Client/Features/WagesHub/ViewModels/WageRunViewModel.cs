using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class WageRunViewModel : ViewModelBase
    {
        private readonly IWageService _wageService;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;

        public WageRunViewModel(IWageService wageService, IDialogService dialogService, IPdfService pdfService)
        {
            _wageService = wageService;
            _dialogService = dialogService;
            _pdfService = pdfService;
            
            // Default period: Current Week's Monday
            // If today is Monday, use today. If today is Sunday, go back to previous Monday?
            // "Wage run will be run on a Wednesday... The run will be fortnight so it will start on a monday."
            // If we run on Wednesday of Week 2, the cycle started on Monday of Week 1.
            // So we take the current Monday, and subtract 7 days.
            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            StartDate = today.AddDays(-1 * diff).AddDays(-7).Date;
            
            // EndDate is calculated in OnStartDateChanged (StartDate + 13 days)
            
            // EndDate is calculated in OnStartDateChanged
        }

        [ObservableProperty]
        private DateTime _startDate;
        
        partial void OnStartDateChanged(DateTime value)
        {
            // Fortnight run: StartDate + 13 days (Total 14 days)
            EndDate = value.AddDays(13);
        }
        
        [ObservableProperty]
        private DateTime _endDate;
        
        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private string _selectedPayType = "Hourly";

        public ObservableCollection<string> PayTypeOptions { get; } = new ObservableCollection<string>
        {
            "Hourly",
            "MonthlySalary"
        };

        [ObservableProperty]
        private string _selectedBranch = "All";

        public ObservableCollection<string> BranchOptions { get; } = new ObservableCollection<string>
        {
            "All",
            "Johannesburg",
            "Cape Town"
        };
        
        [ObservableProperty]
        private decimal _totalGasCharge = 0m;
        
        [ObservableProperty]
        private decimal _defaultSupervisorFee = 500m;

        [ObservableProperty]
        private decimal _companyHousingWashingFee = 0m;

        [ObservableProperty]
        private ObservableCollection<WageRunLineViewModel> _lines = new();

        [ObservableProperty]
        private decimal _grandTotalWage;

        [ObservableProperty]
        private bool _isGenerated;
        

        partial void OnTotalGasChargeChanged(decimal value)
        {
            if (Lines == null || !Lines.Any()) return;
            var housedCount = Lines.Count(x => x.Model.IsCompanyHoused);
            var gasPerPerson = housedCount > 0 ? value / housedCount : 0;
            foreach (var line in Lines.Where(x => x.Model.IsCompanyHoused))
            {
                line.DeductionGas = gasPerPerson;
            }
        }

        partial void OnCompanyHousingWashingFeeChanged(decimal value)
        {
            if (Lines == null || !Lines.Any()) return;
            foreach (var line in Lines.Where(x => x.Model.IsCompanyHoused))
            {
                line.DeductionWashing = value;
            }
        }

        partial void OnDefaultSupervisorFeeChanged(decimal value)
        {
            if (Lines == null || !Lines.Any()) return;
            foreach (var line in Lines.Where(x => x.Model.IsSupervisor))
            {
                line.IncentiveSupervisor = value;
            }
        }

        [ObservableProperty]
        private double _gridZoom = 1.0;

        private Guid? _currentDraftId;
        private OCC.Shared.Models.WageRun? _currentDraft;

        [RelayCommand]
        private async Task GenerateDraft()
        {
            try
            {
                BusyText = "Generating draft run...";
                IsBusy = true;
                
                // Keep track of existing manual edits before regenerating
                var existingEdits = new Dictionary<Guid, (decimal Washing, decimal SupFee)>();
                if (IsGenerated && Lines.Any())
                {
                    foreach (var line in Lines)
                    {
                        existingEdits[line.Model.EmployeeId] = (line.DeductionWashing, line.IncentiveSupervisor);
                    }
                }

                _currentDraft = await _wageService.GenerateDraftRunAsync(StartDate, EndDate, SelectedPayType, SelectedBranch, TotalGasCharge, DefaultSupervisorFee, CompanyHousingWashingFee, Notes);
                _currentDraftId = _currentDraft.Id;
                
                Lines.Clear();
                int index = 1;

                // Robust Consolidation: Group by Name and Id to catch orphan records or inconsistent IDs
                var consolidatedLines = _currentDraft.Lines
                    .GroupBy(l => new { Name = l.EmployeeName?.Trim(), Id = l.EmployeeId })
                    .Select(g => 
                    {
                        var first = g.First();
                        if (g.Count() > 1)
                        {
                            foreach (var extra in g.Skip(1))
                            {
                                first.TotalWage += extra.TotalWage;
                                first.IncentiveSupervisor += extra.IncentiveSupervisor;
                                first.DeductionLoan += extra.DeductionLoan;
                                first.DeductionTax += extra.DeductionTax;
                                first.DeductionWashing += extra.DeductionWashing;
                                first.DeductionGas += extra.DeductionGas;
                                first.DeductionOther += extra.DeductionOther;
                                first.DeductionPPE += extra.DeductionPPE;
                                if (!string.IsNullOrEmpty(extra.VarianceNotes))
                                    first.VarianceNotes = (first.VarianceNotes + " " + extra.VarianceNotes).Trim();
                                
                                // Merge hours as well
                                first.NormalHours += extra.NormalHours;
                                first.Overtime15Hours += extra.Overtime15Hours;
                                first.Overtime20Hours += extra.Overtime20Hours;
                            }
                        }
                        return first;
                    })
                    .OrderBy(l => l.EmployeeName);

                foreach (var line in consolidatedLines)
                {
                    // Re-apply existing edits if they exist
                    if (existingEdits.TryGetValue(line.EmployeeId, out var edits))
                    {
                        line.DeductionWashing = edits.Washing;
                        line.IncentiveSupervisor = edits.SupFee;
                    }

                    var vm = new WageRunLineViewModel(line) { IndexNum = index++ };
                    vm.PropertyChanged += (s, e) => 
                    {
                        if (e.PropertyName == nameof(WageRunLineViewModel.NetPay) || e.PropertyName == nameof(WageRunLineViewModel.IncentiveSupervisor))
                        {
                             UpdateGrandTotal();
                        }
                    };
                    Lines.Add(vm);
                }

                UpdateGrandTotal();
                IsGenerated = true;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to generate draft: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateGrandTotal()
        {
            GrandTotalWage = Lines.Sum(x => x.NetPay);
        }

        [RelayCommand]
        private async Task FinalizeRun()
        {
            if (_currentDraftId == null) return;
            
            var confirm = await _dialogService.ShowConfirmationAsync("Finalize Wage Run", 
                "Are you sure you want to finalize this run? \n\nThis will lock the attendance records and variances for future runs.");
            
            if (!confirm) return;

            try
            {
                BusyText = "Finalizing run...";
                IsBusy = true;
                
                // Update the model lines from the VM lines before sending
                if (_currentDraft != null)
                {
                    _currentDraft.Lines = Lines.Select(vm => vm.Model).ToList();
                    _currentDraft.Notes = Notes; // Ensure latest notes are included
                    
                    var finalizedRun = await _wageService.FinalizeRunAsync(_currentDraft);
                    await _dialogService.ShowAlertAsync("Success", $"Wage Run Finalized Successfully (ID: {finalizedRun.Id}).");
                }
                
                // Clear state
                Lines.Clear();
                IsGenerated = false;
                _currentDraftId = null;
                _currentDraft = null;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to finalize: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PrintPdf()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                BusyText = "Generating PDF...";
                IsBusy = true;

                // Sync VM lines back to model for the PDF
                var runToPrint = _currentDraft ?? new OCC.Shared.Models.WageRun
                {
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Branch = SelectedBranch,
                    PayType = SelectedPayType,
                    Lines = Lines.Select(l => l.Model).ToList()
                };

                if (_currentDraft != null)
                {
                    runToPrint.Lines = Lines.Select(l => l.Model).ToList();
                }

                var path = await _pdfService.GenerateWageRunPdfAsync(runToPrint);
                
                // Open the PDF
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to generate PDF: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveDraft()
        {
             await _dialogService.ShowAlertAsync("Information", "Drafts are now held in-memory until Finalized. Any changes you make here will be included when you click Finalize.");
        }
    }
}
