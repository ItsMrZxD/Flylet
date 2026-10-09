using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flylet.Core.Helpers;
using Flylet.UI;

namespace Flylet.Views
{
    /// <summary>
    /// Pick which part of the background photo shows: drag to move, scroll to zoom, turn it a quarter turn.
    /// The frame has the shape of a flyout card, and what's inside it is what the flyouts draw.
    /// </summary>
    public partial class AdjustPhotoWindow : Window
    {
        private const double StageWidth = 440;
        private const double StageHeight = 260;
        private const double FrameWidth = 320;
        private const double FrameHeight = FrameWidth * 80 / 120;

        private readonly UIManager uiManager = FlyoutHandler.Instance.UIManager;

        private Rect view = new Rect(0, 0, 1, 1);
        private Point? dragStart;

        public AdjustPhotoWindow()
        {
            InitializeComponent();

            double frameLeft = (StageWidth - FrameWidth) / 2;
            double frameTop = (StageHeight - FrameHeight) / 2;
            Canvas.SetLeft(FrameBorder, frameLeft);
            Canvas.SetTop(FrameBorder, frameTop);
            FrameBorder.Width = FrameWidth;
            FrameBorder.Height = FrameHeight;
            Dimmer.Data = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(0, 0, StageWidth, StageHeight)),
                new RectangleGeometry(new Rect(frameLeft, frameTop, FrameWidth, FrameHeight), 8, 8));

            Loaded += (_, _) =>
            {
                uiManager.PropertyChanged += UIManager_PropertyChanged;
                Refresh();
            };
            Closed += (_, _) => uiManager.PropertyChanged -= UIManager_PropertyChanged;
        }

        private void UIManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(UIManager.FlyoutBackgroundImage)
                or nameof(UIManager.FlyoutBackgroundImageZoom)
                or nameof(UIManager.FlyoutBackgroundImagePositionX)
                or nameof(UIManager.FlyoutBackgroundImagePositionY))
            {
                Refresh();
            }
        }

        /// <summary>
        /// Lays the photo out so the part the flyouts show lands exactly in the frame.
        /// </summary>
        private void Refresh()
        {
            var image = uiManager.FlyoutBackgroundImage;
            Photo.Source = image;
            if (image == null || image.Height <= 0)
                return;

            view = BackgroundImageHelper.GetViewbox(
                image.Width / image.Height,
                FrameWidth / FrameHeight,
                uiManager.FlyoutBackgroundImageZoom / 100,
                uiManager.FlyoutBackgroundImagePositionX / 100,
                uiManager.FlyoutBackgroundImagePositionY / 100);

            Photo.Width = FrameWidth / view.Width;
            Photo.Height = FrameHeight / view.Height;
            Canvas.SetLeft(Photo, (StageWidth - FrameWidth) / 2 - view.X * Photo.Width);
            Canvas.SetTop(Photo, (StageHeight - FrameHeight) / 2 - view.Y * Photo.Height);
        }

        private void Stage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(Stage);
            Stage.CaptureMouse();
        }

        private void Stage_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragStart is not Point start || !Stage.IsMouseCaptured)
                return;

            var now = e.GetPosition(Stage);
            Pan(now.X - start.X, now.Y - start.Y);
            dragStart = now;
        }

        private void Stage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStart = null;
            Stage.ReleaseMouseCapture();
        }

        private void Stage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            uiManager.FlyoutBackgroundImageZoom += e.Delta > 0 ? 10 : -10;
        }

        /// <summary>
        /// Dragging moves the photo with the pointer, so the shown part moves the opposite way.
        /// A direction with no room to move (the photo already fills it) stays put.
        /// </summary>
        private void Pan(double dx, double dy)
        {
            if (Photo.Width <= 0 || Photo.Height <= 0)
                return;

            if (view.Width < 1)
            {
                double x = uiManager.FlyoutBackgroundImagePositionX / 100 - dx / Photo.Width / (1 - view.Width);
                uiManager.FlyoutBackgroundImagePositionX = Math.Clamp(x, 0, 1) * 100;
            }

            if (view.Height < 1)
            {
                double y = uiManager.FlyoutBackgroundImagePositionY / 100 - dy / Photo.Height / (1 - view.Height);
                uiManager.FlyoutBackgroundImagePositionY = Math.Clamp(y, 0, 1) * 100;
            }
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e) => uiManager.RotateFlyoutBackgroundImage(false);

        private void RotateRight_Click(object sender, RoutedEventArgs e) => uiManager.RotateFlyoutBackgroundImage(true);

        private void Reset_Click(object sender, RoutedEventArgs e) => uiManager.ResetFlyoutBackgroundImageFraming();

        private void Done_Click(object sender, RoutedEventArgs e) => Close();
    }
}
