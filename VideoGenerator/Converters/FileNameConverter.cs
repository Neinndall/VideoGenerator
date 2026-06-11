using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace VideoGenerator.Converters
{
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
            return "Default Background";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
