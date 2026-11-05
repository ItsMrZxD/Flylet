using Flylet.Core.Helpers;
using Flylet.Core.Interop;
using Flylet.Views;
using System;
using System.Globalization;

namespace Flylet.Helpers
{
    /// <summary>
    /// Shows the support card once ever, as a flyout goes away so it never covers one.
    /// </summary>
    internal static class SupportPromptHelper
    {
        private static int flyoutsThisRun;

        public static bool ShownThisRun { get; private set; }

        public static void OnFlyoutHidden(bool whatsNewShownThisRun)
        {
            if (AppDataHelper.SupportPromptHandled)
                return;

            flyoutsThisRun++;

            DateTime? firstRun = DateTime.TryParse(AppDataHelper.FirstRunUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;

            if (SupportPromptPolicy.ShouldShow(false, firstRun, DateTime.UtcNow, flyoutsThisRun, whatsNewShownThisRun,
                RatePromptHelper.AttemptedThisRun, NativeMethods.IsWindowFullscreen()))
            {
                AppDataHelper.SupportPromptHandled = true;
                ShownThisRun = true;
                new SupportPromptWindow().Show();
            }
        }
    }
}
