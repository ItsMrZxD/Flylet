using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Flylet.Views
{
    public partial class SupportPromptWindow : Window
    {
        private const int ScreenMargin = 16;

        private readonly DispatcherTimer closeTimer = new() { Interval = TimeSpan.FromSeconds(30) };

        public SupportPromptWindow()
        {
            InitializeComponent();

            SizeChanged += (_, _) => PlaceInCorner();
            closeTimer.Tick += (_, _) =>
            {
                if (!IsMouseOver)
                    Close();
            };
            Closed += (_, _) => closeTimer.Stop();
            closeTimer.Start();
        }

        private void PlaceInCorner()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - ScreenMargin;
            Top = area.Bottom - ActualHeight - ScreenMargin;
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            // The app has no global error handler, so a link with nothing to open it must not crash it
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = (string)((FrameworkElement)sender).Tag,
                    UseShellExecute = true
                });
            }
            catch
            {
            }

            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
