using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static Flylet.Core.Interop.NativeMethods;

namespace Flylet.Core.Interop
{
    /// <summary>
    /// A low-level keyboard hook that's only installed while something listens to it (the lock keys flyout).
    /// Every key press on the PC waits for this hook, on the thread that installed it, so it shouldn't be
    /// there when nothing needs it. Subscribe and unsubscribe on the UI thread: the hook needs its message loop.
    /// </summary>
    public class KeyboardHook
    {
        private KeyDownEventHandler keyDown;

        public event KeyDownEventHandler KeyDown
        {
            add { keyDown += value; UpdateHook(); }
            remove { keyDown -= value; UpdateHook(); }
        }

        public delegate void KeyDownEventHandler(Key key, int virtualKey);

        private KeyUpEventHandler keyUp;

        public event KeyUpEventHandler KeyUp
        {
            add { keyUp += value; UpdateHook(); }
            remove { keyUp -= value; UpdateHook(); }
        }

        public delegate void KeyUpEventHandler(Key key, int virtualKey);

        private const int WH_KEYBOARD_LL = 13;
        private const int HC_ACTION = 0;
        private const int WM_KEYDOWN = 0x100;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYDOWN = 0x104;
        private const int WM_SYSKEYUP = 0x105;

        private HookProc _procDelegate;
        private IntPtr HHookID = IntPtr.Zero;

        #region Properties

        public Key? CurrentKey { get; private set; }

        #endregion

        public KeyboardHook()
        {
            _procDelegate = new HookProc(KeyboardProc);
        }

        private void UpdateHook()
        {
            bool needed = keyDown != null || keyUp != null;

            if (needed && HHookID == IntPtr.Zero)
            {
                var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                IntPtr handle = GetModuleHandle(module.ModuleName);
                HHookID = (IntPtr)SetWindowsHookEx(WH_KEYBOARD_LL, _procDelegate, handle, 0);

                // Throwing here used to crash Flylet at startup; without the hook only the lock keys flyout is missing
                if (HHookID == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"{nameof(KeyboardHook)}: Could not set keyboard hook");
                }
            }
            else if (!needed && HHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx((int)HHookID);
                HHookID = IntPtr.Zero;
                CurrentKey = null;
            }
        }

        private int KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    var vkcode = (int)((KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))).vkCode;
                    var key = KeyInterop.KeyFromVirtualKey(vkcode);
                    CurrentKey = key;
                    keyDown?.Invoke(key, vkcode);
                }
                if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    var vkcode = (int)((KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))).vkCode;
                    var key = KeyInterop.KeyFromVirtualKey(vkcode);
                    CurrentKey = null;
                    keyUp?.Invoke(key, vkcode);
                }
            }

            return CallNextHookEx((int)IntPtr.Zero, nCode, wParam, lParam);
        }

        ~KeyboardHook()
        {
            if (HHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx((int)HHookID);
            }
        }
    }
}
