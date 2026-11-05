using ModernWpf;
using System;
using System.Globalization;
using System.Windows;

namespace Flylet.UI.Media
{
    public class BindablePoint : DependencyObject
    {
        public static double MaxXValue = int.MaxValue;
        public static double MaxYValue = int.MaxValue;

        #region Properties

        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register(
                nameof(X),
                typeof(double),
                typeof(BindablePoint),
                new PropertyMetadata(0.0, OnXPropertyChanged));

        public double X
        {
            get => (double)GetValue(XProperty);
            set => SetValue(XProperty, value);
        }

        private static void OnXPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePoint point)
            {
                point.OnValueChanged();
            }
        }

        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register(
                nameof(Y),
                typeof(double),
                typeof(BindablePoint),
                new PropertyMetadata(0.0, OnYPropertyChanged));

        public double Y
        {
            get => (double)GetValue(YProperty);
            set => SetValue(YProperty, value);
        }

        private static void OnYPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePoint point)
            {
                point.OnValueChanged();
            }
        }

        #endregion

        public event TypedEventHandler<BindablePoint, EventArgs> ValueChanged;

        public BindablePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public BindablePoint() : this(0.0, 0.0)
        {
        }

        private void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public static bool TryParse(string value, out BindablePoint result)
        {
            result = new();
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrWhiteSpace(value))
            {
                // Saved as "x, y". Up to 0.12 the numbers used the user's own format, so a comma-decimal
                // language wrote "1262,8, 456,8": split on ", " and accept that format too, but never
                // with thousands separators, which turned 1262.8 into 12628 and put the flyout off-screen
                var values = value.Split(", ");
                if (values.Length == 2)
                {
                    var vX = values[0].Trim();
                    var vY = values[1].Trim();

                    if (TryParseNumber(vX, out double x) && TryParseNumber(vY, out double y))
                    {
                        result.X = x;
                        result.Y = y;

                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryParseNumber(string value, out double result) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);

        public Point ToPoint()
        {
            return new(X, Y);
        }

        public override string ToString()
        {
            return string.Create(CultureInfo.InvariantCulture, $"{X}, {Y}");
        }
    }
}
