using Flylet.DesignTime;
using Flylet.Properties;
using System.Windows;
using System.Windows.Controls;

namespace Flylet.Navigation
{
    public partial class AudioModulePage : Page
    {
        public AudioModulePage()
        {
            InitializeComponent();

            Loaded += AudioModulePage_Loaded;
        }

        private void AudioModulePage_Loaded(object sender, RoutedEventArgs e)
        {
            // The preview shows the song that's playing, so the options are judged on real art and text
            PreviewSessionControl.DataContext = (object)FlyoutHandler.Instance.AudioFlyoutHelper.FirstMediaSession
                ?? new MockMediaSession(Strings.MediaCard_SampleTitle, Strings.MediaCard_SampleArtist, Program.AppName);
        }
    }
}
