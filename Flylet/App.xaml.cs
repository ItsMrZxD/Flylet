using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Flylet
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Startup += App_Startup;
            JumpListHelper.CreateJumpList();

            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(ComboBox_PreviewMouseWheel));
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            FlyoutHandler.Instance = new FlyoutHandler();
            FlyoutHandler.Instance.Initialize();
        }

        /// <summary>
        /// A closed combo box that has focus changes its selection on the mouse wheel, so scrolling down
        /// Settings right after picking something silently picked the next items too. The wheel scrolls
        /// the page instead; an open drop-down still scrolls its own list.
        /// </summary>
        private static void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen || e.Handled)
                return;

            e.Handled = true;
            if (VisualTreeHelper.GetParent(comboBox) is UIElement parent)
            {
                parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = comboBox
                });
            }
        }
    }
}
