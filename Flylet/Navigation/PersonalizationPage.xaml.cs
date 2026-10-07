using Flylet.DesignTime;
using Flylet.Properties;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Flylet.Navigation
{
    public partial class PersonalizationPage : Page
    {
        private MockMediaSession sampleMediaSession;

        public PersonalizationPage()
        {
            InitializeComponent();

            Loaded += PersonalizationPage_Loaded;
            Unloaded += PersonalizationPage_Unloaded;
        }

        private void PersonalizationPage_Loaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated += AudioFlyoutHelper_MediaSessionsUpdated;
            UpdatePreviewSession();
        }

        private void PersonalizationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            FlyoutHandler.Instance.AudioFlyoutHelper.MediaSessionsUpdated -= AudioFlyoutHelper_MediaSessionsUpdated;
        }

        private void AudioFlyoutHelper_MediaSessionsUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdatePreviewSession);
        }

        private void ChooseFlyoutBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Settings_FlyoutBackgroundImageFilter}|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.jxr",
                Title = Strings.Settings_ChooseFlyoutBackgroundImage
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            bool imported = FlyoutHandler.Instance.UIManager.SetFlyoutBackgroundImage(dialog.FileName);
            FlyoutBackgroundImageErrorText.Visibility = imported ? Visibility.Collapsed : Visibility.Visible;
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
