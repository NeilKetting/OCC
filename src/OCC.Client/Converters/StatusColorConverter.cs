using Avalonia.Data.Converters;
using Avalonia.Media;
using OCC.Shared.Models;
using System;
using System.Globalization;

namespace OCC.Client.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is InventoryStatus invStatus && parameter is string param)
            {
                if (param == "Background")
                {
                    return invStatus switch
                    {
                        InventoryStatus.OK => Brushes.LightGreen, // #90EE90
                        InventoryStatus.Low => Brushes.Bisque, // #FFE4C4
                        InventoryStatus.Critical => Brushes.LightCoral, // #F08080
                        _ => Brushes.Transparent
                    };
                }
                else if (param == "Foreground")
                {
                    return invStatus switch
                    {
                        InventoryStatus.OK => Brushes.DarkGreen,
                        InventoryStatus.Low => Brushes.DarkOrange,
                        InventoryStatus.Critical => Brushes.DarkRed,
                        _ => Brushes.Black
                    };
                }
            }
            else if (value is string statusStr)
            {
                return statusStr switch
                {
                    "Open" => Brushes.IndianRed,
                    "In Progress" => Brushes.Orange,
                    "Planning" => Brushes.SlateGray,
                    "Feature Update" => Brushes.DeepSkyBlue,
                    "Fixed" => Brushes.DodgerBlue,
                    "Waiting for Client" => Brushes.Goldenrod,
                    "Resolved" => Brushes.MediumSeaGreen,
                    "Closed" => Brushes.LightGray,
                    _ => Brushes.Transparent
                };
            }
            
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
