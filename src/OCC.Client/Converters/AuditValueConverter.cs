using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia.Data.Converters;

namespace OCC.Client.Converters
{
    public class AuditValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string jsonStr && !string.IsNullOrWhiteSpace(jsonStr))
            {
                try
                {
                    // Attempt to parse simple JSON object
                    // Example input: { "Name": "Johnny" }
                    // Desired output: Name: Johnny
                    
                    var sb = new StringBuilder();
                    using (JsonDocument doc = JsonDocument.Parse(jsonStr))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in doc.RootElement.EnumerateObject())
                            {
                                sb.AppendLine($"{property.Name}: {property.Value}");
                            }
                        }
                        else
                        {
                            return jsonStr; // Not an object, return raw
                        }
                    }
                    return sb.ToString().TrimEnd();
                }
                catch
                {
                    // Failed to parse, return raw string
                    return jsonStr;
                }
            }
            return "-"; // Default for null/empty
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Read-only converter, do not support writing back.
            // Return DoNothing to indicate no value should be set on the source.
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
