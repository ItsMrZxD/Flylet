using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// Finds a mixer row's icon and Start-menu name off the UI thread. Icon order: the icon the stream names itself
    /// (System sounds does), the app's Start-menu icon, the program file's own icon. No icon = null, and the row
    /// shows a letter. Results are kept per app.
    /// </summary>
    internal static class AppAudioIcons
    {
        // Drawn at 20 px; 40 px stays sharp up to 200% scaling
        private const int IconSize = 40;

        private static readonly Dictionary<string, (ImageSource Icon, string StartMenuName)> cache = new();

        /// <param name="keepName">The stream named itself; that name wins over the Start-menu one.</param>
        public static void Load(AppAudioEntry entry, string sessionIconPath, bool keepName, Dispatcher dispatcher)
        {
            if (cache.TryGetValue(entry.Key, out var hit))
            {
                Apply(entry, hit.Icon, keepName ? null : hit.StartMenuName);
                return;
            }

            uint pid = entry.ProcessId;
            string exePath = entry.ExePath;
            bool system = entry.IsSystemSounds;

            Task.Run(() =>
            {
                string startMenuName = null;
                ImageSource icon = FromIconResource(sessionIconPath);

                if (!system)
                {
                    string appId = AppAudioIdentity.GetAppId(pid);
                    string name = AppAudioIdentity.GetStartMenuName(appId);
                    if (AppAudioNaming.IsUsableStartMenuName(name, appId))
                    {
                        startMenuName = name;
                        icon ??= AppAudioIdentity.GetStartMenuIcon(appId, IconSize);
                    }
                    icon ??= AppAudioIdentity.GetFileIcon(exePath, IconSize);
                }

                dispatcher.BeginInvoke(() =>
                {
                    cache[entry.Key] = (icon, startMenuName);
                    Apply(entry, icon, keepName ? null : startMenuName);
                });
            });
        }

        private static void Apply(AppAudioEntry entry, ImageSource icon, string name)
        {
            entry.Icon = icon;
            if (!string.IsNullOrWhiteSpace(name))
                entry.Name = name;
        }

        /// <summary>
        /// An icon given as "@%SystemRoot%\System32\AudioSrv.Dll,-203" (file, resource id).
        /// </summary>
        private static ImageSource FromIconResource(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            string text = Environment.ExpandEnvironmentVariables(iconPath.Trim().TrimStart('@'));
            int index = 0;
            int comma = text.LastIndexOf(',');
            if (comma > 0 && int.TryParse(text[(comma + 1)..], out int parsed))
            {
                index = parsed;
                text = text[..comma];
            }

            IntPtr large = IntPtr.Zero;
            try
            {
                if (ExtractIconEx(text, index, out large, IntPtr.Zero, 1) == 0 || large == IntPtr.Zero)
                    return null;
                var source = Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch { return null; }
            finally
            {
                if (large != IntPtr.Zero)
                    DestroyIcon(large);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
