using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flylet.Controls
{
    public partial class ColorSwatchPicker : UserControl
    {
        public static readonly Color[] DefaultSwatches =
        {
            Color.FromRgb(0xE7, 0x48, 0x56),
            Color.FromRgb(0xFF, 0x8C, 0x00),
            Color.FromRgb(0xFF, 0xB9, 0x00),
            Color.FromRgb(0x0B, 0x6A, 0x0B),
            Color.FromRgb(0x00, 0xB7, 0xC3),
            Color.FromRgb(0x00, 0x78, 0xD7),
            Color.FromRgb(0x87, 0x64, 0xB8),
            Color.FromRgb(0xC2, 0x39, 0xB3),
        };

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorSwatchPicker),
                new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public static readonly DependencyProperty SwatchesProperty =
            DependencyProperty.Register(
                nameof(Swatches),
                typeof(IEnumerable<Color>),
                typeof(ColorSwatchPicker),
                new PropertyMetadata(DefaultSwatches));

        public IEnumerable<Color> Swatches
        {
            get => (IEnumerable<Color>)GetValue(SwatchesProperty);
            set => SetValue(SwatchesProperty, value);
        }

        // Full 6-digit #RRGGBB only: rejects the 3/4/8-digit shorthands WPF's ColorConverter also
        // accepts, so a color can never pick up alpha the hex box doesn't show, and a value never
        // commits (and rewrites the box, fighting the caret) until the user's typed a complete one
        private static readonly Regex HexColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        private bool _updatingHexBox;

        public ColorSwatchPicker()
        {
            InitializeComponent();
            UpdateHexBoxText();
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorSwatchPicker)d).UpdateHexBoxText();
        }

        private void UpdateHexBoxText()
        {
            if (HexBox == null)
            {
                return;
            }

            _updatingHexBox = true;
            HexBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
            _updatingHexBox = false;
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                SelectedColor = color;
            }
        }

        private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingHexBox || !HexColorPattern.IsMatch(HexBox.Text))
            {
                return;
            }

            SelectedColor = (Color)ColorConverter.ConvertFromString(HexBox.Text);
        }
    }
}
