using Flylet.Core.Helpers;
using Flylet.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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

        /// <summary>
        /// Shows a color as #RRGGBB (swatch tooltips).
        /// </summary>
        public static readonly IValueConverter HexConverter = new ColorToHexConverter();

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

        private const double ShadeKnobSize = 16;
        private const double HueKnobWidth = 10;

        private bool _updatingHexBox;
        private bool _updatingPickerHexBox;

        // The picker's own color while it's open; kept as HSV so the hue survives dragging to white or black
        private double _hue;
        private double _saturation;
        private double _value;

        // The saved-color slot the open picker writes to
        private int _editingSlot = -1;

        private static UIManager UIManager => FlyoutHandler.Instance?.UIManager;

        public ColorSwatchPicker()
        {
            InitializeComponent();
            UpdateHexBoxText();
            UpdateSlots();

            // Both pickers show the same saved colors, so each listens while it's on screen
            Loaded += (_, _) =>
            {
                if (UIManager != null)
                    UIManager.PropertyChanged += UIManager_PropertyChanged;
                UpdateSlots();
            };
            Unloaded += (_, _) =>
            {
                if (UIManager != null)
                    UIManager.PropertyChanged -= UIManager_PropertyChanged;
            };
        }

        private void UIManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UI.UIManager.SavedColors))
                UpdateSlots();
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (ColorSwatchPicker)d;
            picker.UpdateHexBoxText();
            picker.UpdateSlots();
        }

        private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private void UpdateHexBoxText()
        {
            if (HexBox == null)
            {
                return;
            }

            _updatingHexBox = true;
            HexBox.Text = ToHex(SelectedColor);
            _updatingHexBox = false;
        }

        private void UpdateSlots()
        {
            if (SlotsList == null)
            {
                return;
            }

            var saved = UIManager?.SavedColors ?? new string[UI.UIManager.SavedColorCount];
            var selected = SelectedColor;
            var slots = new List<SavedColorSlot>();
            for (int i = 0; i < saved.Count; i++)
            {
                Color? color = saved[i] != null && AccentColorHelper.TryParseColor(saved[i], out var c) ? c : null;
                slots.Add(new SavedColorSlot(i, color,
                    color is Color s && s.R == selected.R && s.G == selected.G && s.B == selected.B));
            }
            SlotsList.ItemsSource = slots;
        }

        private static SavedColorSlot SlotOf(object sender) => (sender as FrameworkElement)?.DataContext as SavedColorSlot;

        private void Slot_Click(object sender, RoutedEventArgs e)
        {
            if (SlotOf(sender) is not SavedColorSlot slot)
                return;

            if (slot.Color is Color color)
                SelectedColor = color;
            else
                OpenPicker((FrameworkElement)sender, slot.Index, SelectedColor);
        }

        private void SlotChange_Click(object sender, RoutedEventArgs e)
        {
            if (SlotOf(sender) is SavedColorSlot { Color: Color color } slot
                && (sender as MenuItem)?.Parent is ContextMenu { PlacementTarget: FrameworkElement target })
            {
                OpenPicker(target, slot.Index, color);
            }
        }

        private void SlotRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SlotOf(sender) is SavedColorSlot slot)
                UIManager?.SetSavedColor(slot.Index, null);
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

            if (AccentColorHelper.TryParseColor(HexBox.Text, out var color))
            {
                SelectedColor = color;
            }
        }

        #region Picker popup

        private void OpenPicker(FrameworkElement target, int slot, Color start)
        {
            _editingSlot = slot;
            (_hue, _saturation, _value) = AccentColorHelper.ColorToHsv(start);
            PickerError.Visibility = Visibility.Collapsed;
            UpdatePicker(updateHexBox: true);
            PickerPopup.PlacementTarget = target;
            PickerPopup.IsOpen = true;
            PickerHexBox.Focus();
            PickerHexBox.SelectAll();
        }

        private void UpdatePicker(bool updateHexBox)
        {
            var color = AccentColorHelper.ColorFromHsv(_hue, _saturation, _value);

            ShadeHue.Background = new SolidColorBrush(AccentColorHelper.ColorFromHsv(_hue, 1, 1));
            PickerPreview.Background = new SolidColorBrush(color);

            Canvas.SetLeft(ShadeKnob, _saturation * ShadeArea.Width - ShadeKnobSize / 2);
            Canvas.SetTop(ShadeKnob, (1 - _value) * ShadeArea.Height - ShadeKnobSize / 2);
            Canvas.SetLeft(HueKnob, _hue / 360 * (HueArea.Width - HueKnobWidth));

            if (updateHexBox)
            {
                _updatingPickerHexBox = true;
                PickerHexBox.Text = ToHex(color);
                _updatingPickerHexBox = false;
            }
        }

        private void SetShadeFromPoint(Point point)
        {
            _saturation = Math.Clamp(point.X / ShadeArea.Width, 0, 1);
            _value = 1 - Math.Clamp(point.Y / ShadeArea.Height, 0, 1);
            PickerError.Visibility = Visibility.Collapsed;
            UpdatePicker(updateHexBox: true);
        }

        private void SetHueFromPoint(Point point)
        {
            // Just under 360 so the knob can reach the right end without wrapping to red's 0
            _hue = Math.Clamp((point.X - HueKnobWidth / 2) / (HueArea.Width - HueKnobWidth), 0, 1) * 359.9;
            PickerError.Visibility = Visibility.Collapsed;
            UpdatePicker(updateHexBox: true);
        }

        private void ShadeArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShadeArea.CaptureMouse();
            SetShadeFromPoint(e.GetPosition(ShadeArea));
        }

        private void ShadeArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (ShadeArea.IsMouseCaptured)
            {
                SetShadeFromPoint(e.GetPosition(ShadeArea));
            }
        }

        private void HueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HueArea.CaptureMouse();
            SetHueFromPoint(e.GetPosition(HueArea));
        }

        private void HueArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (HueArea.IsMouseCaptured)
            {
                SetHueFromPoint(e.GetPosition(HueArea));
            }
        }

        private void Area_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private static string NormalizeHex(string text)
        {
            text = text.Trim();
            return text.Length > 0 && text[0] != '#' ? "#" + text : text;
        }

        private void PickerHexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingPickerHexBox)
            {
                return;
            }

            PickerError.Visibility = Visibility.Collapsed;
            string hex = NormalizeHex(PickerHexBox.Text);
            if (HexColorPattern.IsMatch(hex) && AccentColorHelper.TryParseColor(hex, out var color))
            {
                (_hue, _saturation, _value) = AccentColorHelper.ColorToHsv(color);
                UpdatePicker(updateHexBox: false);
            }
        }

        private void PickerDone_Click(object sender, RoutedEventArgs e) => CommitPicker();

        private void PickerCancel_Click(object sender, RoutedEventArgs e) => PickerPopup.IsOpen = false;

        private void CommitPicker()
        {
            string hex = NormalizeHex(PickerHexBox.Text);
            if (!HexColorPattern.IsMatch(hex) || !AccentColorHelper.TryParseColor(hex, out var color))
            {
                PickerError.Visibility = Visibility.Visible;
                return;
            }

            // Done saves the color in its slot and uses it
            UIManager?.SetSavedColor(_editingSlot, ToHex(color));
            SelectedColor = color;
            PickerPopup.IsOpen = false;
        }

        private void Picker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                PickerPopup.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                CommitPicker();
                e.Handled = true;
            }
        }

        #endregion

        private sealed class ColorToHexConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                => value is Color color ? ToHex(color) : null;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// One saved-color slot as the picker shows it.
    /// </summary>
    public sealed class SavedColorSlot
    {
        public SavedColorSlot(int index, Color? color, bool isSelected)
        {
            Index = index;
            Color = color;
            IsSelected = isSelected;
            Brush = color is Color c ? new SolidColorBrush(c) : null;
        }

        public int Index { get; }

        public Color? Color { get; }

        public bool IsEmpty => Color == null;

        public bool IsFilled => Color != null;

        public bool IsSelected { get; }

        public Brush Brush { get; }

        public string ToolTip => Color is Color c
            ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
            : Flylet.Properties.Strings.ColorPicker_Custom;
    }

    /// <summary>
    /// Visible when the two colors match (alpha ignored): the ring on the swatch that's in use.
    /// </summary>
    public sealed class ColorEqualsToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.Length == 2 && values[0] is Color a && values[1] is Color b
                && a.R == b.R && a.G == b.G && a.B == b.B
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
