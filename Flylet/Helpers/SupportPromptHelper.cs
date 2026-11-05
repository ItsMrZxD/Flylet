using Flylet.Core.Helpers;
using Flylet.Core.Interop;
using Flylet.Views;

namespace Flylet.Helpers
{
    /// <summary>
    /// Shows the support card once ever, as a flyout goes away so it never covers one.
    /// </summary>
    internal static class SupportPromptHelper
    {
        private static int flyoutsThisRun;

        public static bool ShownThisRun { get; private set; }

        public static void OnFlyoutHidden(bool whatsNewShowing)
        {
            if (AppDataHelper.SupportPromptHandled)
                return;

            flyoutsThisRun++;

            if (SupportPromptPolicy.ShouldShow(false, flyoutsThisRun, whatsNewShowing,
                RatePromptHelper.AttemptedThisRun, NativeMethods.IsWindowFullscreen()))
            {
                AppDataHelper.SupportPromptHandled = true;
                ShownThisRun = true;
                new SupportPromptWindow().Show();
            }
        }
    }
}
