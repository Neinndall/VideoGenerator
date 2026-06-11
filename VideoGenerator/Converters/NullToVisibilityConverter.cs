using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VideoGenerator.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null || (value is string s && string.IsNullOrEmpty(s));
            bool invert = parameter != null && parameter.ToString() == "Invert";

            if (invert)
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
