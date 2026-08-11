using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VideoGenerator.Views.Converters
{
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            
            bool result = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v && v == Visibility.Visible)
            {
                return parameter;
            }
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
