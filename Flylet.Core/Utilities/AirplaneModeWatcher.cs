using System;
using System.Diagnostics;
using System.Management;

namespace Flylet.Core.Utilities
{
    public class AirplaneModeWatcher
    {
        private readonly ManagementEventWatcher watcher;

        public event EventHandler<AirplaneModeChangedEventArgs> Changed;

        public AirplaneModeWatcher()
        {
            try
            {
                WqlEventQuery query = new(
                     "SELECT * FROM RegistryValueChangeEvent WHERE " +
                     "Hive = 'HKEY_LOCAL_MACHINE'" +
                     @"AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\RadioManagement\\SystemRadioState' AND ValueName=''");

                watcher = new ManagementEventWatcher(query);
                watcher.EventArrived += new EventArrivedEventHandler(HandleEvent);
            }
            catch (Exception managementException)
            {
                Debug.WriteLine($"{nameof(AirplaneModeWatcher)}: " + managementException.Message);
            }
        }

        // WMI can be disabled or broken; the airplane flyout just won't follow changes then
        public void Start()
        {
            try { watcher?.Start(); } catch (Exception ex) { Debug.WriteLine($"{nameof(AirplaneModeWatcher)}: " + ex.Message); }
        }

        public void Stop()
        {
            try { watcher?.Stop(); } catch (Exception ex) { Debug.WriteLine($"{nameof(AirplaneModeWatcher)}: " + ex.Message); }
        }

        public static bool GetIsAirplaneModeEnabled()
        {
            var regkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState", false);
            return (int)regkey?.GetValue("", 1) == 1;
        }

        private void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            Changed?.Invoke(this, new AirplaneModeChangedEventArgs(GetIsAirplaneModeEnabled()));
        }
    }

    public class AirplaneModeChangedEventArgs : EventArgs
    {
        public AirplaneModeChangedEventArgs(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }
    }
}
