using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

namespace Flylet.Core.Utilities
{
    public static class RadioAvailability
    {
        /// <summary>
        /// Checks whether this PC has any Wi-Fi or Bluetooth radio hardware.
        /// </summary>
        /// <remarks>
        /// A desktop with no wireless hardware still reports phantom Wireless80211 network
        /// interfaces (Windows' WiFi Direct virtual adapters), so those are only counted when
        /// their <see cref="OperationalStatus"/> isn't <see cref="OperationalStatus.NotPresent"/>.
        /// </remarks>
        public static bool HasAnyRadios()
        {
            try
            {
                bool hasWifi = NetworkInterface.GetAllNetworkInterfaces().Any(ni =>
                    ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    ni.OperationalStatus != OperationalStatus.NotPresent);

                if (hasWifi)
                {
                    return true;
                }

                using var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'");
                using var results = searcher.Get();
                return results.Count > 0;
            }
            catch
            {
                // A slow-starting or broken WMI service, or a NetworkInformationException, shouldn't
                // crash startup over a hardware-detection nicety - just assume there's nothing to find.
                return false;
            }
        }
    }
}
