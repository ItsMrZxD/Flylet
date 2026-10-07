using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flylet.Controls
{
    /// <summary>
    /// The background of a flyout card, clipped to the card's corners.
    /// </summary>
    public partial class FlyoutBackgroundLayer : UserControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(FlyoutBackgroundLayer),
                new PropertyMetadata(default(CornerRadius), (d, _) => ((FlyoutBackgroundLayer)d).UpdateClip()));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public FlyoutBackgroundLayer()
        {
            InitializeComponent();

            SizeChanged += (_, _) => UpdateClip();
        }

        private void UpdateClip()
        {
            double radius = CornerRadius.TopLeft;
            RootGrid.Clip = new RectangleGeometry(new Rect(RenderSize), radius, radius);
        }
    }
}
