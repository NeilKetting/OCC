using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.Linq;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class WageRunLineViewModel : ObservableObject
    {
        [ObservableProperty]
        private WageRunLine _model;

        private int? _index;
        public int? IndexNum
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public WageRunLineViewModel(WageRunLine model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        // --- 27 COLUMNS AS PER IMAGE ---
        
        // 1. #
        public string Index => IndexNum?.ToString() ?? string.Empty;

        // 2. BAS
        public string EmployeeNumber => Model?.EmployeeNumber ?? string.Empty;

        // 3. NAME
        public string EmployeeName => Model?.EmployeeName?.ToUpper() ?? string.Empty;

        // 4. RATE P/HR
        public decimal? RatePHrDisplay => Model?.HourlyRate;

        // 5. HRS
        public double? HrsDisplay => Model?.NormalHours;

        // 6. STD O/T RATE (1.5x)
        public decimal? StdOtRate => Model?.HourlyRate * 1.5m;

        // 7. SAT O/T RATE (1.5x)
        public decimal? SatOtRate => Model?.HourlyRate * 1.5m;

        // 8. SUN-P'HOL RATE (2.0x)
        public decimal? SunPHolRate => Model?.HourlyRate * 2.0m;

        // 9. DEC RATE - REMOVED PER USER

        // 10. DEC HRS - REMOVED PER USER

        // 11. DEC TOTAL
        public decimal? DecTotal => 0;

        // 12. $TD O/T (Hours)
        public double? StdOt => Model?.Overtime15Hours;

        // 13. SAT O/T (Hours)
        public double? SatOt => 0;

        // 14. SUN O/T (Hours)
        public double? SunOt => Model?.Overtime20Hours;

        // 15. LOANS
        public string DeductionLoanDisplay => (Model?.DeductionLoan > 0 ? Model.DeductionLoan.ToString("F2") : string.Empty);

        // 16. WASHING
        public string DeductionWashingDisplay => (Model?.DeductionWashing > 0 ? Model.DeductionWashing.ToString("F2") : string.Empty);

        // 17. GAS
        public string DeductionGasDisplay => (Model?.DeductionGas > 0 ? Model.DeductionGas.ToString("F2") : string.Empty);

        // 18. OTHER
        public string OtherDisplay => (Model?.DeductionOther > 0) ? Model.DeductionOther.ToString("F2") : string.Empty;

        public bool HasSupervisorFee => Model?.IncentiveSupervisor > 0;

        // --- COMPUTED DISPLAY SECTION ---
        public decimal? RatePDayDisplay => Model?.HourlyRate * 8.75m;
        public int? DaysWeek1Display => (int?)(Model?.DaysWorkedWeek1 ?? 0);
        public int? DaysWeek2Display => (int?)(Model?.DaysWorkedWeek2 ?? 0);
        public int? TotalDaysDisplay => (int?)(Model?.TotalDaysWorked ?? 0);
        public double? HrsPDayDisplay => 8.75;

        // ----------------------------------------

        public void RefreshTotalNett()
        {
            RecalculateTotalWage();
            OnPropertyChanged(nameof(NetPay));
            OnPropertyChanged(nameof(TotalRem));
            // Trigger UI updates for string-formatted displays
            OnPropertyChanged(nameof(DeductionLoanDisplay));
            OnPropertyChanged(nameof(DeductionWashingDisplay));
            OnPropertyChanged(nameof(DeductionGasDisplay));
            OnPropertyChanged(nameof(OtherDisplay));
            OnPropertyChanged(nameof(DeductionPPEDisplay));
            OnPropertyChanged(nameof(IncentiveSupervisor));
        }

        private void RecalculateTotalWage()
        {
            if (Model == null) return;
            Model.TotalWage = (decimal)(Model.NormalHours + Model.ProjectedHours + Model.VarianceHours) * Model.HourlyRate +
                             (decimal)Model.Overtime15Hours * Model.HourlyRate * 1.5m +
                             (decimal)Model.Overtime20Hours * Model.HourlyRate * 2.0m;
        }

        // --- Editable Properties for spreadsheet-style corrections ---

        public double NormalHours
        {
            get => Model?.NormalHours ?? 0;
            set { if (Model != null && Math.Abs(Model.NormalHours - value) > 0.001) { Model.NormalHours = value; RefreshTotalNett(); OnPropertyChanged(); OnPropertyChanged(nameof(HrsDisplay)); } }
        }

        public double Overtime15Hours
        {
            get => Model?.Overtime15Hours ?? 0;
            set { if (Model != null && Math.Abs(Model.Overtime15Hours - value) > 0.001) { Model.Overtime15Hours = value; RefreshTotalNett(); OnPropertyChanged(); OnPropertyChanged(nameof(StdOt)); } }
        }

        public double Overtime20Hours
        {
            get => Model?.Overtime20Hours ?? 0;
            set { if (Model != null && Math.Abs(Model.Overtime20Hours - value) > 0.001) { Model.Overtime20Hours = value; RefreshTotalNett(); OnPropertyChanged(); OnPropertyChanged(nameof(SunOt)); } }
        }

        public decimal DeductionLoan
        {
            get => Model?.DeductionLoan ?? 0;
            set { if (Model != null && Model.DeductionLoan != value) { Model.DeductionLoan = value; RefreshTotalNett(); OnPropertyChanged(); } }
        }

        public decimal DeductionPPE
        {
            get => Model?.DeductionPPE ?? 0;
            set { if (Model != null && Model.DeductionPPE != value) { Model.DeductionPPE = value; RefreshTotalNett(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionPPEDisplay)); } }
        }

        public string DeductionPPEDisplay => (Model?.DeductionPPE > 0 ? Model.DeductionPPE.ToString("F2") : string.Empty);

        public decimal DeductionOther
        {
            get => Model?.DeductionOther ?? 0;
            set { if (Model != null && Model.DeductionOther != value) { Model.DeductionOther = value; RefreshTotalNett(); OnPropertyChanged(); OnPropertyChanged(nameof(OtherDisplay)); } }
        }

        public decimal DeductionGas
        {
            get => Model?.DeductionGas ?? 0;
            set { if (Model != null && Model.DeductionGas != value) { Model.DeductionGas = value; RefreshTotalNett(); OnPropertyChanged(); } }
        }

        public decimal DeductionWashing
        {
            get => Model?.DeductionWashing ?? 0;
            set { if (Model != null && Model.DeductionWashing != value) { Model.DeductionWashing = value; RefreshTotalNett(); OnPropertyChanged(); } }
        }

        public decimal IncentiveSupervisor
        {
            get => Model?.IncentiveSupervisor ?? 0;
            set { if (Model != null && Model.IncentiveSupervisor != value) { Model.IncentiveSupervisor = value; RefreshTotalNett(); OnPropertyChanged(); } }
        }

        public decimal NetPay => Model?.NetPay ?? 0;
        public decimal TotalRem => (Model?.NetPay ?? 0) + (Model?.IncentiveSupervisor ?? 0);
        public double VarianceHours => Model?.VarianceHours ?? 0;
        public decimal HourlyRate => Model?.HourlyRate ?? 0;
        public decimal TotalWage => Model?.TotalWage ?? 0;
        public string Branch => Model?.Branch ?? string.Empty;
    }
}
