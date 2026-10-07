using System;
using System.Globalization;
using System.Windows.Data;

namespace Flylet.Converters
{
    /// <summary>
    /// Turns (position, start, end, width) into the width of the filled part of the thin timeline.
    /// </summary>
    public class TimelineFractionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 4
                && values[0] is TimeSpan position
                && values[1] is TimeSpan start
                && values[2] is TimeSpan end
                && values[3] is double width
                && end > start)
            {
                double fraction = (position - start).TotalSeconds / (end - start).TotalSeconds;
                return Math.Clamp(fraction, 0.0, 1.0) * width;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
