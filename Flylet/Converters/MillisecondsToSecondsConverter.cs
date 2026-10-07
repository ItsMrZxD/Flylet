using System;
using System.Globalization;
using System.Windows.Data;

namespace Flylet.Converters
{
    /// <summary>
    /// Shows a millisecond setting as seconds (2750 -> 2.75) and stores it back in whole 50 ms steps.
    /// </summary>
    public class MillisecondsToSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int milliseconds ? milliseconds / 1000.0 : 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double seconds ? (int)(Math.Round(seconds * 20) * 50) : Binding.DoNothing;
        }
    }
}
