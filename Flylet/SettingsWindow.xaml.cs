using Flylet.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace Flylet
{
    public partial class SettingsWindow : Window
    {
        private bool _isActive;

        public SettingsWindow()
        {
            InitializeComponent();

            WindowPlacementHelper.SetPlacement(new WindowInteropHelper(this).EnsureHandle(), AppDataHelper.SettingsWindowPlacement);
        }

        protected override void OnActivated(EventArgs e)
        {
            if (!_isActive)
            {
                Workarounds.RenderLoopFix.ApplyFix();
                _isActive = true;
            }

            base.OnActivated(e);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            _isActive = false;

            base.OnDeactivated(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            AppDataHelper.SettingsWindowPlacement = WindowPlacementHelper.GetPlacement(new WindowInteropHelper(this).Handle);
            e.Cancel = true;
            Hide();

            base.OnClosing(e);
        }
    }
}
