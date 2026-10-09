using Flylet.Core.Helpers;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;

namespace Flylet.Helpers
{
    /// <summary>
    /// Asks for a Store rating once, with the Store's own dialog, after a few days of use.
    /// Ratings drive the app's visibility in the Store, so this is worth doing, but only once.
    /// </summary>
    internal static class RatePromptHelper
    {
        // The dialog is attempted once per run; a failed attempt (no Store, offline) retries next run
        private static bool attemptedThisRun;

        /// <summary>
        /// Stamps the first run and counts flyouts. Counting stops once the prompt is settled.
        /// </summary>
        public static void Initialize()
        {
            if (string.IsNullOrEmpty(AppDataHelper.FirstRunUtc))
            {
                AppDataHelper.FirstRunUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            }
        }

        public static void CountFlyoutShown()
        {
            if (AppDataHelper.RatePromptHandled)
                return;

            int count = AppDataHelper.FlyoutsShownCount;
            if (count < RatePromptPolicy.MinFlyoutsShown)
            {
                AppDataHelper.FlyoutsShownCount = count + 1;
            }
        }

        /// <summary>
        /// Shows the rating dialog over <paramref name="owner"/> if it's time. Safe to call often.
        /// </summary>
        public static async void TryPrompt(Window owner)
        {
            if (attemptedThisRun || !IsTime())
                return;

            attemptedThisRun = true;

            try
            {
                var store = StoreContext.GetDefault();
                WinRT.Interop.InitializeWithWindow.Initialize(store, new WindowInteropHelper(owner).EnsureHandle());

                var result = await store.RequestRateAndReviewAppAsync();

                // Rated or dismissed both count as asked. Errors (offline, no Store listing, as in
                // the dev build) don't, so the next run tries again.
                if (result.Status is StoreRateAndReviewStatus.Succeeded or StoreRateAndReviewStatus.CanceledByUser)
                {
                    AppDataHelper.RatePromptHandled = true;
                }
            }
            catch
            {
                // Never let a rating prompt bother the user
            }
        }

        private static bool IsTime()
        {
            DateTime? firstRun = DateTime.TryParse(AppDataHelper.FirstRunUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;

            return RatePromptPolicy.ShouldPrompt(firstRun, DateTime.UtcNow, AppDataHelper.FlyoutsShownCount, AppDataHelper.RatePromptHandled);
        }
    }
}
