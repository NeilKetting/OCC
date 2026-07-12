using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace OCC.WpfClient.Infrastructure.Converters
{
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path)
            {
                try
                {
                    var fileName = Path.GetFileName(path);
                    if (fileName.Length > 37 && fileName[36] == '_')
                    {
                        return fileName.Substring(37);
                    }
                    return fileName;
                }
                catch
                {
                    return path;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
