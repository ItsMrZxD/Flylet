using System.Windows;

namespace Flylet.Input
{
    internal sealed class TappedRoutedEventArgs : RoutedEventArgs
    {
        public TappedRoutedEventArgs()
        {
        }

        internal int Timestamp { get; set; }
    }
}
