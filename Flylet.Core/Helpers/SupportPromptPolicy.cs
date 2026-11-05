using System;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// When to show the support card: once ever, a day after installing, after a little use in this run,
    /// and never next to another prompt or over a fullscreen app.
    /// </summary>
    public static class SupportPromptPolicy
    {
        public const int MinDaysSinceFirstRun = 1;

        public const int MinFlyoutsThisRun = 3;

        public static bool ShouldShow(bool alreadyHandled, DateTime? firstRunUtc, DateTime nowUtc, int flyoutsThisRun,
            bool whatsNewShownThisRun, bool ratingAttemptedThisRun, bool fullscreen)
        {
            if (alreadyHandled || firstRunUtc is not DateTime firstRun)
                return false;

            return nowUtc - firstRun >= TimeSpan.FromDays(MinDaysSinceFirstRun)
                && flyoutsThisRun >= MinFlyoutsThisRun
                && !whatsNewShownThisRun
                && !ratingAttemptedThisRun
                && !fullscreen;
        }
    }
}
