using System;
using System.Globalization;
using System.IO;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// The plain rules behind the volume mixer's rows: which streams belong to one app, what the row is called and
    /// which letter stands in when an app has no icon.
    /// </summary>
    public static class AppAudioNaming
    {
        public const string SystemSoundsKey = "system";

        /// <summary>
        /// Streams with the same key share one row. Same program file = same app (a browser often has several
        /// streams); without a path, the process is all we have.
        /// </summary>
        public static string GroupKey(bool isSystemSounds, string exePath, uint processId)
        {
            if (isSystemSounds)
                return SystemSoundsKey;
            if (!string.IsNullOrWhiteSpace(exePath))
                return exePath.Trim().ToLowerInvariant();
            return "pid:" + processId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The best name we have, from best to worst: the name the app gave its sound, the program's description,
        /// the program's file name, the process name, then "App" with the process id.
        /// </summary>
        public static string PickName(string sessionDisplayName, string fileDescription, string exePath,
            string processName, uint processId)
        {
            // Session names like "@%SystemRoot%\System32\AudioSrv.Dll,-202" are resource links, not names
            if (!string.IsNullOrWhiteSpace(sessionDisplayName) && !sessionDisplayName.TrimStart().StartsWith("@"))
                return sessionDisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(fileDescription))
                return fileDescription.Trim();
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                string file = Path.GetFileNameWithoutExtension(exePath.Trim());
                if (!string.IsNullOrWhiteSpace(file))
                    return file;
            }
            if (!string.IsNullOrWhiteSpace(processName))
                return processName.Trim();
            return "App " + processId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Windows hands back the raw id (often a path like "{GUID}\Steam\steam.exe") when an app has no Start-menu
        /// entry; only a real name counts.
        /// </summary>
        public static bool IsUsableStartMenuName(string name, string appId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            name = name.Trim();
            return !string.Equals(name, appId?.Trim(), StringComparison.OrdinalIgnoreCase)
                && name.IndexOfAny(new[] { '\\', '/' }) < 0
                && !name.StartsWith("{")
                && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The letter shown in a circle when an app has no icon: the first letter or digit of its name, upper case.
        /// </summary>
        public static string InitialOf(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var e = StringInfo.GetTextElementEnumerator(name);
                while (e.MoveNext())
                {
                    string element = e.GetTextElement();
                    if (char.IsLetterOrDigit(element, 0))
                        return element.ToUpper(CultureInfo.CurrentCulture);
                }
            }
            return "?";
        }

        /// <summary>
        /// Order of the rows: apps by name, system sounds last (like Windows' own mixer).
        /// </summary>
        public static int Compare(bool aIsSystem, string aName, bool bIsSystem, string bName)
        {
            if (aIsSystem != bIsSystem)
                return aIsSystem ? 1 : -1;
            return string.Compare(aName, bName, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
