using System;
using System.Runtime.InteropServices;

namespace Flylet.Core.Display
{
    /// <summary>
    /// EnumDisplayDevices returns the English fallback of a monitor's description
    /// ("Generic PnP Monitor"); the device manager returns it in the display language.
    /// </summary>
    public static class LocalizedDeviceName
    {
        private const uint CR_SUCCESS = 0;
        private const string InterfacePrefix = @"\\?\";

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        // DEVPKEY_NAME: friendly name, falling back to the device description.
        private static readonly DEVPROPKEY DevPKeyName = new()
        {
            fmtid = new Guid("b725f130-47ef-101a-a5f1-02608c9eebac"),
            pid = 10
        };

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_DevNode_PropertyW(
            uint devInst, ref DEVPROPKEY propertyKey, out uint propertyType,
            byte[] buffer, ref uint bufferSize, uint flags);

        /// <param name="deviceInterfacePath">A display device ID such as \\?\DISPLAY#MSI5CD0#7&amp;4478184&amp;0&amp;UID268#{guid}.</param>
        public static string Get(string deviceInterfacePath, string fallback)
        {
            try
            {
                string instanceId = ToInstanceId(deviceInterfacePath);
                if (instanceId == null
                    || CM_Locate_DevNodeW(out uint devInst, instanceId, 0) != CR_SUCCESS)
                {
                    return fallback;
                }

                var key = DevPKeyName;
                uint size = 0;
                CM_Get_DevNode_PropertyW(devInst, ref key, out _, null, ref size, 0);
                if (size < 2)
                {
                    return fallback;
                }

                var buffer = new byte[size];
                if (CM_Get_DevNode_PropertyW(devInst, ref key, out _, buffer, ref size, 0) != CR_SUCCESS)
                {
                    return fallback;
                }

                string name = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                return string.IsNullOrWhiteSpace(name) ? fallback : name;
            }
            catch
            {
                return fallback;
            }
        }

        public static string ToInstanceId(string deviceInterfacePath)
        {
            if (string.IsNullOrEmpty(deviceInterfacePath)
                || !deviceInterfacePath.StartsWith(InterfacePrefix, StringComparison.Ordinal))
            {
                return null;
            }

            string path = deviceInterfacePath.Substring(InterfacePrefix.Length);
            int guid = path.LastIndexOf("#{", StringComparison.Ordinal);
            if (guid >= 0)
            {
                path = path.Substring(0, guid);
            }

            return path.Replace('#', '\\');
        }
    }
}
