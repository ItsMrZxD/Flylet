using System;

namespace Flylet.Helpers
{
    internal static class PackUriHelper
    {
        public static Uri GetAbsoluteUri(string path)
        {
            return new Uri($"pack://application:,,,/Flylet;component/{path}");
        }
    }
}
