using Flylet.Core.AppInformation;
using Flylet.Core.Helpers;
using Flylet.Core.Interop;
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
    /// Finds a mixer row's icon (and, for Store apps, their real name). In order: the icon the stream itself names
    /// (System sounds does), the Store app's logo, the program file's own icon. No icon at all = null, and the row
    /// falls back to a letter. Results are kept per app.
    /// </summary>
    internal static class AppAudioIcons
    {
        private static readonly Dictionary<string, (ImageSource Icon, string Name)> cache = new();

        public static void Load(AppAudioEntry entry, string sessionIconPath, Dispatcher dispatcher)
        {
            if (cache.TryGetValue(entry.Key, out var hit))
            {
                Apply(entry, hit.Icon, hit.Name);
                return;
            }

            // Store apps: the logo and name from their package, like the media card does
            if (!entry.IsSystemSounds && NativeMethods.IsImmersiveProcess(entry.ProcessId))
            {
                var info = SourceAppInfo.FromProcessId(entry.ProcessId);
                if (info != null)
                {
                    info.InfoFetched += (_, _) => dispatcher.BeginInvoke(() =>
                    {
                        ImageSource logo = null;
                        try
                        {
                            if (BitmapHelper.TryCreateBitmapImageFromStream(info.LogoStream, out var bitmap))
                            {
                                bitmap.Freeze();
                                logo = bitmap;
                            }
                        }
                        catch { }

                        string name = string.IsNullOrWhiteSpace(info.DisplayName) ? null : info.DisplayName;
                        info.Dispose();

                        if (logo != null)
                            Store(entry, logo, name);
                        else
                            LoadFromFiles(entry, sessionIconPath, dispatcher, name);
                    });
                    info.FetchInfosAsync();
                    return;
                }
            }

            LoadFromFiles(entry, sessionIconPath, dispatcher, null);
        }

        private static void LoadFromFiles(AppAudioEntry entry, string sessionIconPath, Dispatcher dispatcher, string name)
        {
            string exePath = entry.ExePath;
            Task.Run(() =>
            {
                ImageSource icon = FromIconResource(sessionIconPath) ?? FromProgramFile(exePath);
                dispatcher.BeginInvoke(() => Store(entry, icon, name));
            });
        }

        private static void Store(AppAudioEntry entry, ImageSource icon, string name)
        {
            cache[entry.Key] = (icon, name);
            Apply(entry, icon, name);
        }

        private static void Apply(AppAudioEntry entry, ImageSource icon, string name)
        {
            entry.Icon = icon;
            if (name != null)
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
            return Extract(text, index);
        }

        /// <summary>
        /// The program file's own icon; null if the file has none (Windows would show a blank program icon).
        /// </summary>
        private static ImageSource FromProgramFile(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return null;
            return Extract(exePath, 0);
        }

        private static ImageSource Extract(string file, int index)
        {
            IntPtr large = IntPtr.Zero;
            try
            {
                if (ExtractIconEx(file, index, out large, IntPtr.Zero, 1) == 0 || large == IntPtr.Zero)
                    return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
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
