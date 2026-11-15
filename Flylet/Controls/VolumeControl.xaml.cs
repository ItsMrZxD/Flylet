using Flylet.Core.Audio;
using Flylet.UI;
using Flylet.Utilities;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flylet.Controls
{
    public partial class VolumeControl : UserControl
    {
        private AppAudioMixer mixer;
        private UIManager uiManager;

        public VolumeControl()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => HookMixer(DataContext as AppAudioMixer);
            Loaded += (_, _) => HookUIManager();
            UpdateMixerPanel();
        }

        private void HookMixer(AppAudioMixer newMixer)
        {
            if (mixer != null)
                mixer.PropertyChanged -= Mixer_PropertyChanged;
            mixer = newMixer;
            if (mixer != null)
                mixer.PropertyChanged += Mixer_PropertyChanged;
            UpdateMixerPanel();
        }

        private void HookUIManager()
        {
            var manager = FlyoutHandler.Instance?.UIManager;
            if (manager == null || manager == uiManager)
                return;
            if (uiManager != null)
                uiManager.PropertyChanged -= UIManager_PropertyChanged;
            uiManager = manager;
            uiManager.PropertyChanged += UIManager_PropertyChanged;
            UpdateMixerPanel();
        }

        private void Mixer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppAudioMixer.HasApps))
                UpdateMixerPanel();
        }

        private void UIManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UIManager.VolumeMixerExpanded))
                UpdateMixerPanel();
        }

        private bool Expanded => uiManager?.VolumeMixerExpanded ?? false;

        private void UpdateMixerPanel()
        {
            bool expanded = Expanded;
            MixerPanel.Visibility = expanded && mixer?.HasApps == true ? Visibility.Visible : Visibility.Collapsed;
            MixerGlyph.Glyph = expanded ? CommonGlyphs.ChevronUp : CommonGlyphs.ChevronDown;

            string tip = expanded ? Properties.Strings.VolumeMixer_Hide : Properties.Strings.VolumeMixer_Show;
            MixerButton.ToolTip = tip;
            AutomationProperties.SetName(MixerButton, tip);
        }

        private void MixerButton_Click(object sender, RoutedEventArgs e)
        {
            HookUIManager();
            if (uiManager != null)
                uiManager.VolumeMixerExpanded = !uiManager.VolumeMixerExpanded;
        }

        private void AppMuteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppAudioEntry app)
                app.Muted = !app.Muted;
        }

        private void AppSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppAudioEntry app)
            {
                app.Volume += e.Delta / 120.0;
                e.Handled = true;
            }
        }
    }
}
