using System.Diagnostics;
using System.Windows.Controls;

namespace Flylet.Navigation
{
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void RateAndReviewButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = "ms-windows-store://review/?ProductId=9npss6nw7t23",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}
