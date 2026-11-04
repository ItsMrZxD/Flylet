using System;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// Whether to show the one-time "What's new" note after a Flylet update.
    /// </summary>
    public static class WhatsNewPolicy
    {
        /// <summary>
        /// Returns true only when <paramref name="currentVersion"/> is newer than
        /// <paramref name="lastSeenVersion"/>.
        /// A fresh install (no last seen version), an unparsable version, or an
        /// equal/older current version returns false. Versions are compared as
        /// <see cref="Version"/>, not as text, so 0.12.0 is newer than 0.9.0.
        /// This never throws.
        /// </summary>
        public static bool ShouldShow(string lastSeenVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(lastSeenVersion)
                || !Version.TryParse(lastSeenVersion, out var lastSeen)
                || !Version.TryParse(currentVersion, out var current))
            {
                return false;
            }

            return current > lastSeen;
        }
    }
}
