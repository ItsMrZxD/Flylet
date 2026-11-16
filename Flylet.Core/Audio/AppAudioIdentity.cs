using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// Asks Windows who an app is the way the Start menu sees it: its app id, its Start-menu name and its icon at
    /// a set size. Works the same for normal and Store apps, without searching through installed packages.
    /// Idea from EarTrumpet (MIT); own code.
    /// </summary>
    internal static class AppAudioIdentity
    {
        /// <summary>
        /// The app's id in the Start menu (Store apps: their AUMID; other apps: the id Windows gives them, often
        /// the program path). Null if Windows doesn't know.
        /// </summary>
        public static string GetAppId(uint processId)
        {
            string aumid = GetPackagedAppId(processId);
            if (!string.IsNullOrEmpty(aumid))
                return aumid;

            try
            {
                var resolver = (IApplicationResolver)new ApplicationResolver();
                try
                {
                    resolver.GetAppIDForProcess(processId, out string appId, out _, out _, out _);
                    return string.IsNullOrWhiteSpace(appId) ? null : appId;
                }
                finally { Marshal.ReleaseComObject(resolver); }
            }
            catch { return null; }
        }

        private static string GetPackagedAppId(uint processId)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero)
                return null;
            try
            {
                int length = 512;
                var buffer = new StringBuilder(length);
                return GetApplicationUserModelId(process, ref length, buffer) == 0 ? buffer.ToString() : null;
            }
            finally { CloseHandle(process); }
        }

        /// <summary>
        /// The Start-menu entry for an app id, or null when the id has no entry.
        /// </summary>
        private static IShellItem GetAppsFolderItem(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return null;
            try
            {
                var iid = typeof(IShellItem).GUID;
                return SHCreateItemInKnownFolder(FOLDERID_AppsFolder, KF_FLAG_DONT_VERIFY, appId, ref iid) as IShellItem;
            }
            catch { return null; }
        }

        public static string GetStartMenuName(string appId)
        {
            var item = GetAppsFolderItem(appId);
            if (item == null)
                return null;
            try
            {
                item.GetDisplayName(SIGDN_NORMALDISPLAY, out IntPtr name);
                try { return Marshal.PtrToStringUni(name); }
                finally { Marshal.FreeCoTaskMem(name); }
            }
            catch { return null; }
            finally { Marshal.ReleaseComObject(item); }
        }

        /// <summary>
        /// The Start-menu icon for an app id, <paramref name="size"/> pixels square.
        /// </summary>
        public static ImageSource GetStartMenuIcon(string appId, int size)
        {
            var item = GetAppsFolderItem(appId);
            if (item == null)
                return null;
            try { return GetImage(item, size); }
            finally { Marshal.ReleaseComObject(item); }
        }

        /// <summary>
        /// A file's icon (a program's own icon), <paramref name="size"/> pixels square. Null when the file has no
        /// icon of its own (Windows would draw a blank program icon).
        /// </summary>
        public static ImageSource GetFileIcon(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path) || ExtractIconEx(path, -1, IntPtr.Zero, IntPtr.Zero, 0) == 0)
                return null;
            try
            {
                var iid = typeof(IShellItem).GUID;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out object obj) != 0 || obj is not IShellItem item)
                    return null;
                try { return GetImage(item, size); }
                finally { Marshal.ReleaseComObject(item); }
            }
            catch { return null; }
        }

        private static ImageSource GetImage(IShellItem item, int size)
        {
            IntPtr bitmap = IntPtr.Zero;
            try
            {
                ((IShellItemImageFactory)item).GetImage(new SIZE { cx = size, cy = size }, SIIGBF_RESIZETOFIT | SIIGBF_ICONONLY, out bitmap);
                if (bitmap == IntPtr.Zero)
                    return null;
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch { return null; }
            finally
            {
                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);
            }
        }

        /// <summary>
        /// Turns a link like "@%SystemRoot%\System32\AudioSrv.Dll,-202" into its text; anything else comes back as is.
        /// </summary>
        public static string LoadIndirectString(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("@"))
                return text;
            var buffer = new StringBuilder(512);
            return SHLoadIndirectString(text.Trim(), buffer, buffer.Capacity, IntPtr.Zero) == 0 ? buffer.ToString() : text;
        }

        /// <summary>
        /// False once the process has ended. A process we can't open (access denied) counts as running.
        /// </summary>
        public static bool IsProcessRunning(uint processId)
        {
            IntPtr process = OpenProcess(SYNCHRONIZE, false, processId);
            if (process == IntPtr.Zero)
                // ERROR_INVALID_PARAMETER: no process has this id any more
                return Marshal.GetLastWin32Error() != 87;
            try { return WaitForSingleObject(process, 0) == WAIT_TIMEOUT; }
            finally { CloseHandle(process); }
        }

        /// <summary>
        /// Calls <paramref name="exited"/> (on a pool thread) when the process ends. Returns null if it can't be
        /// watched. Dispose the result to stop watching. No thread waits per process: the pool watches the handle.
        /// </summary>
        public static IDisposable WatchProcessExit(uint processId, Action exited)
        {
            IntPtr process = OpenProcess(SYNCHRONIZE, false, processId);
            if (process == IntPtr.Zero)
                return null;
            var handle = new ProcessHandle(process);
            var registration = ThreadPool.RegisterWaitForSingleObject(handle, (_, _) => exited(), null, Timeout.Infinite, true);
            return new ExitWatch(handle, registration);
        }

        private sealed class ProcessHandle : WaitHandle
        {
            public ProcessHandle(IntPtr handle) => SafeWaitHandle = new SafeWaitHandle(handle, true);
        }

        private sealed class ExitWatch : IDisposable
        {
            private readonly WaitHandle handle;
            private readonly RegisteredWaitHandle registration;

            public ExitWatch(WaitHandle handle, RegisteredWaitHandle registration)
            {
                this.handle = handle;
                this.registration = registration;
            }

            public void Dispose()
            {
                registration.Unregister(null);
                handle.Dispose();
            }
        }

        #region Interop

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint SYNCHRONIZE = 0x00100000;
        private const uint WAIT_TIMEOUT = 0x102;
        private const uint KF_FLAG_DONT_VERIFY = 0x00004000;
        private const uint SIGDN_NORMALDISPLAY = 0;
        private const int SIIGBF_RESIZETOFIT = 0x0;
        private const int SIIGBF_ICONONLY = 0x4;
        private static readonly Guid FOLDERID_AppsFolder = new("1e87508d-89c2-42f0-8a7e-645a0f50ca58");

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetApplicationUserModelId(IntPtr process, ref int length, StringBuilder appId);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateItemInKnownFolder([In] in Guid folderId, uint flags, string itemName, ref Guid iid);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out object item);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string file, int index, IntPtr large, IntPtr small, uint count);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int SHLoadIndirectString(string source, StringBuilder output, int outputLength, IntPtr reserved);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid iid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint sigdn, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            void GetImage(SIZE size, int flags, out IntPtr bitmap);
        }

        [ComImport, Guid("660b90c8-73a9-4b58-8cae-355b7f55341b")]
        private class ApplicationResolver { }

        // Undocumented (Windows 8+). Only GetAppIDForProcess is used; the first three slots are placeholders.
        [ComImport, Guid("de25675a-72de-44b4-9373-05170450c140"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationResolver
        {
            void GetAppIDForShortcut();
            void GetAppIDForShortcutObject();
            void GetAppIDForWindow();
            void GetAppIDForProcess(uint processId, [MarshalAs(UnmanagedType.LPWStr)] out string appId,
                out int pinningPrevented, out int explicitAppId, out int embeddedShortcutValid);
        }

        #endregion
    }
}
