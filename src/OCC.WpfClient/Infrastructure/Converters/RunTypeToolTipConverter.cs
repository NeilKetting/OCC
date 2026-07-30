using System;
using System.Globalization;
using System.Windows.Data;
using OCC.Shared.Models;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class RunTypeToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WageRunType runType)
            {
                return runType switch
                {
                    WageRunType.Standard => "Standard: Regular payroll run for the selected pay frequency and branch.",
                    WageRunType.AdHocAdvance => "Ad-Hoc Advance: Advance payment run (Mamparra). Net payments made in this run will be automatically recovered as deductions in subsequent wage runs.",
                    WageRunType.Correction => "Correction: Adjustment run to rectify prior wage calculation errors.",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
