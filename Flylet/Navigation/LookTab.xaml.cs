using Flylet.Controls;
using Flylet.DesignTime;
using Flylet.Properties;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Flylet.Core.Helpers;
using Flylet.UI;

namespace Flylet.Navigation
{
    /// <summary>
    /// Settings > Look: theme, colors, photo, media card and top bar, under a live preview.
    /// </summary>
    public partial class LookTab : UserControl
    {
        private MockMediaSession sampleMediaSession;

        public LookTab()
        {
            InitializeComponent();

            // A still volume card for the preview; the real one is driven by the audio module
            PreviewVolumeControl.VolumeSlider.Value = 40;
            PreviewVolumeControl.textVal.Text = "40";

            // The preview's own top bar: the setting decides if it's always there, hidden until pointed at, or gone
            var previewTopBar = new FlyoutTopBar();
            PreviewTopBarHost.Content = previewTopBar;
            PreviewCards.MouseEnter += (_, _) => previewTopBar.SetPointerOverFlyout(true);
            PreviewCards.MouseLeave += (_, _) => previewTopBar.SetPointerOverFlyout(false);

            Loaded += LookTab_Loaded;
            Unloaded += LookTab_Unloaded;
        }

        private void LookTab_Loaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated += AudioFlyoutHelper_MediaSessionsUpdated;
            FlyoutHandler.Instance.UIManager.PropertyChanged += UIManager_PropertyChanged;
            UpdateThumbnailFraming();
            UpdatePreviewSession();
        }

        private void LookTab_Unloaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated -= AudioFlyoutHelper_MediaSessionsUpdated;
            FlyoutHandler.Instance.UIManager.PropertyChanged -= UIManager_PropertyChanged;
        }

        private void AudioFlyoutHelper_MediaSessionsUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdatePreviewSession);
        }

        private void UIManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(UIManager.FlyoutBackgroundImage)
                or nameof(UIManager.FlyoutBackgroundImageZoom)
                or nameof(UIManager.FlyoutBackgroundImagePositionX)
                or nameof(UIManager.FlyoutBackgroundImagePositionY))
            {
                UpdateThumbnailFraming();
            }
        }

        /// <summary>
        /// The thumbnail shows the part of the photo the flyouts show, not just the middle.
        /// </summary>
        private void UpdateThumbnailFraming()
        {
            var uiManager = FlyoutHandler.Instance.UIManager;
            if (uiManager.FlyoutBackgroundImage is not ImageSource image || image.Height <= 0)
                return;

            ThumbnailBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
            ThumbnailBrush.Viewbox = BackgroundImageHelper.GetViewbox(
                image.Width / image.Height,
                120.0 / 80.0,
                uiManager.FlyoutBackgroundImageZoom / 100,
                uiManager.FlyoutBackgroundImagePositionX / 100,
                uiManager.FlyoutBackgroundImagePositionY / 100);
        }

        private void AdjustFlyoutBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            new Views.AdjustPhotoWindow { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private async void ChooseFlyoutBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Settings_FlyoutBackgroundImageFilter}|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.jxr",
                Title = Strings.Settings_ChooseFlyoutBackgroundImage
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            // A big GIF takes a few seconds; one import at a time
            var button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                bool imported = await FlyoutHandler.Instance.UIManager.SetFlyoutBackgroundImageAsync(dialog.FileName);
                FlyoutBackgroundImageErrorText.Visibility = imported ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// The preview follows the song the media keys control, so the options are judged on real art and
        /// text; it falls back to a sample song when nothing is playing.
        /// </summary>
        private void UpdatePreviewSession()
        {
            PreviewSessionControl.DataContext = (object)FlyoutHandler.Instance.AudioFlyoutHelper.PreviewMediaSession
                ?? (sampleMediaSession ??= new MockMediaSession(Strings.MediaCard_SampleTitle, Strings.MediaCard_SampleArtist, Program.AppName));
        }
    }
}
