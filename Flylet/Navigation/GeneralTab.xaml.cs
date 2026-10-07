using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Flylet.Navigation
{
    /// <summary>
    /// Settings > General: startup, language, which flyout to use, tray icon, reset and about.
    /// </summary>
    public partial class GeneralTab : UserControl
    {
        public GeneralTab()
        {
            InitializeComponent();
        }

        private void RateAndReviewButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "ms-windows-store://review/?ProductId=9npss6nw7t23",
                UseShellExecute = true
            });
        }
    }
}
