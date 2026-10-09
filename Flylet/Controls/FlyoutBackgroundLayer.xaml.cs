using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Flylet.Core.Helpers;
using System.Windows.Media;
using Flylet.UI;

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
            PhotoBorder.SizeChanged += (_, _) => UpdatePhotoFraming();
            // Subscribing only while shown keeps cards that are gone from being held by the long-lived UIManager
            Loaded += (_, _) =>
            {
                // WPF can raise Loaded twice without Unloaded in between; never subscribe twice
                FlyoutHandler.Instance.UIManager.PropertyChanged -= UIManager_PropertyChanged;
                FlyoutHandler.Instance.UIManager.PropertyChanged += UIManager_PropertyChanged;
                // A neighbouring card showing, hiding or growing moves this card's part of the photo
                LayoutUpdated -= FlyoutBackgroundLayer_LayoutUpdated;
                LayoutUpdated += FlyoutBackgroundLayer_LayoutUpdated;
                UpdatePhotoFraming();
                UpdateAnimationHold();
            };
            Unloaded += (_, _) =>
            {
                FlyoutHandler.Instance.UIManager.PropertyChanged -= UIManager_PropertyChanged;
                LayoutUpdated -= FlyoutBackgroundLayer_LayoutUpdated;
                UpdateAnimationHold(forceRelease: true);
            };
            IsVisibleChanged += (_, _) => UpdateAnimationHold();
        }

        private bool holdingAnimation;

        /// <summary>
        /// An animated photo only plays while a layer is actually on screen (a hidden flyout window doesn't count).
        /// </summary>
        private void UpdateAnimationHold(bool forceRelease = false)
        {
            bool shouldHold = !forceRelease && IsLoaded && IsVisible;
            if (shouldHold == holdingAnimation)
                return;

            holdingAnimation = shouldHold;
            var uiManager = FlyoutHandler.Instance.UIManager;
            if (shouldHold)
            {
                uiManager.AcquireBackgroundAnimation();
            }
            else
            {
                uiManager.ReleaseBackgroundAnimation();
            }
        }

        private void FlyoutBackgroundLayer_LayoutUpdated(object sender, EventArgs e) => UpdatePhotoFraming();

        private void UIManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(UIManager.FlyoutBackgroundImage):
                case nameof(UIManager.IsFlyoutBackgroundImageShown):
                case nameof(UIManager.FlyoutBackgroundImageZoom):
                case nameof(UIManager.FlyoutBackgroundImagePositionX):
                case nameof(UIManager.FlyoutBackgroundImagePositionY):
                    UpdatePhotoFraming();
                    break;
            }
        }

        /// <summary>
        /// Marks the panel that holds the flyout cards: the photo is laid out over all of them as one picture,
        /// each card showing its own part, instead of every card showing the whole photo.
        /// </summary>
        public static readonly DependencyProperty IsPhotoStackProperty =
            DependencyProperty.RegisterAttached("IsPhotoStack", typeof(bool), typeof(FlyoutBackgroundLayer), new PropertyMetadata(false));

        public static bool GetIsPhotoStack(DependencyObject element) => (bool)element.GetValue(IsPhotoStackProperty);

        public static void SetIsPhotoStack(DependencyObject element, bool value) => element.SetValue(IsPhotoStackProperty, value);

        // The photo border sticks out of the card by this much on every side (Margin in the XAML)
        private const double BlurMargin = 70;

        // Space between cards in the stack; the photo runs through it so the picture stays continuous
        private const double CardSpacing = 8;

        private Rect lastViewbox;

        /// <summary>
        /// Shows this card's part of the photo the user picked. With several cards the photo spans them stacked
        /// top to bottom, in the order they appear; alone, a card shows the photo in its own shape.
        /// </summary>
        private void UpdatePhotoFraming()
        {
            if (PhotoBorder.ActualWidth <= 0 || PhotoBorder.ActualHeight <= 0)
                return;

            // Runs on every layout pass while shown, so do nothing at all without a photo (most people)
            var uiManager = FlyoutHandler.Instance.UIManager;
            if (!uiManager.IsFlyoutBackgroundImageShown || uiManager.FlyoutBackgroundImage is not ImageSource image || image.Height <= 0)
                return;

            double imageAspect = image.Width / image.Height;
            double zoom = uiManager.FlyoutBackgroundImageZoom / 100;
            double positionX = uiManager.FlyoutBackgroundImagePositionX / 100;
            double positionY = uiManager.FlyoutBackgroundImagePositionY / 100;

            Rect viewbox;
            if (TryGetStackLayout(out double stackWidth, out double stackHeight, out double cardTop, out double cardHeight))
            {
                // The whole stack plus the blur margin around it is the area the photo covers
                double areaWidth = stackWidth + 2 * BlurMargin;
                double areaHeight = stackHeight + 2 * BlurMargin;
                var area = BackgroundImageHelper.GetViewbox(imageAspect, areaWidth / areaHeight, zoom, positionX, positionY);
                viewbox = new Rect(
                    area.X,
                    area.Y + area.Height * (cardTop / areaHeight),
                    area.Width * (RenderSize.Width + 2 * BlurMargin) / areaWidth,
                    area.Height * (cardHeight + 2 * BlurMargin) / areaHeight);
            }
            else
            {
                viewbox = BackgroundImageHelper.GetViewbox(
                    imageAspect, PhotoBorder.ActualWidth / PhotoBorder.ActualHeight, zoom, positionX, positionY);
            }

            if (viewbox == lastViewbox && PhotoBrush.ViewboxUnits == BrushMappingMode.RelativeToBoundingBox)
                return;

            lastViewbox = viewbox;
            PhotoBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
            PhotoBrush.Viewbox = viewbox;
        }

        /// <summary>
        /// Finds the card stack this layer belongs to and where this card sits when all visible cards are stacked
        /// top to bottom (cards side by side, like in the Settings preview, are stacked in the same order).
        /// </summary>
        private bool TryGetStackLayout(out double stackWidth, out double stackHeight, out double cardTop, out double cardHeight)
        {
            stackWidth = stackHeight = cardTop = cardHeight = 0;

            DependencyObject child = this;
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !GetIsPhotoStack(parent))
            {
                child = parent;
                parent = VisualTreeHelper.GetParent(child);
            }

            if (parent is not Panel stack || child is not UIElement card)
                return false;

            var cards = new List<(UIElement Element, Point Position, Size Size)>();
            foreach (UIElement element in stack.Children)
            {
                if (!element.IsVisible || element.RenderSize.Height <= 0)
                    continue;

                cards.Add((element, element.TranslatePoint(new Point(), stack), element.RenderSize));
            }

            cards.Sort((a, b) => a.Position.Y != b.Position.Y ? a.Position.Y.CompareTo(b.Position.Y) : a.Position.X.CompareTo(b.Position.X));

            double top = 0;
            bool found = false;
            foreach (var item in cards)
            {
                if (item.Element == card)
                {
                    cardTop = top;
                    cardHeight = item.Size.Height;
                    found = true;
                }

                stackWidth = Math.Max(stackWidth, item.Size.Width);
                top += item.Size.Height + CardSpacing;
            }

            stackHeight = top - CardSpacing;
            return found && stackWidth > 0 && stackHeight > 0;
        }

        private void UpdateClip()
        {
            double radius = CornerRadius.TopLeft;
            RootGrid.Clip = new RectangleGeometry(new Rect(RenderSize), radius, radius);
        }
    }
}
