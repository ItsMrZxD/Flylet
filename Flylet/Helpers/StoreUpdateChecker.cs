using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace Flylet.Helpers
{
    /// <summary>
    /// Checks whether the Microsoft Store has a newer version of this app available.
    /// </summary>
    public static class StoreUpdateChecker
    {
        /// <summary>
        /// Returns true if the Store reports an update. Never throws; each call asks the Store again.
        /// </summary>
        public static async Task<bool> IsUpdateAvailableAsync()
        {
            // Ask the Store only if it has a listing for this build; the dev build has none.
            if (IsDevBuild())
                return false;

            try
            {
                var store = StoreContext.GetDefault();
                var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
                return updates.Count > 0;
            }
            catch
            {
                // Never let an update check bother the user
                return false;
            }
        }

        /// <summary>
        /// Opens Flylet's Store page: the update button and the release notes in the user's language are there.
        /// </summary>
        public static void OpenStorePage()
        {
            // The app has no global error handler, so a PC without the Store app must not crash it here
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "ms-windows-store://pdp/?ProductId=9npss6nw7t23",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        // The dev build (MrzxD.Flylet.Dev) has no Store listing, and asking the Store about it can
        // crash with an access violation inside Windows.ApplicationModel.Store.dll that can't be caught
        // (seen with the rating dialog, see RatePromptHelper)
        private static bool IsDevBuild()
        {
            try
            {
                return Windows.ApplicationModel.Package.Current.Id.Name.EndsWith(".Dev", StringComparison.Ordinal);
            }
            catch
            {
                return true;
            }
        }
    }
}
