using System;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// When to ask for a Store rating: once, after a few days of real use.
    /// </summary>
    public static class RatePromptPolicy
    {
        public const int MinDaysSinceFirstRun = 3;

        public const int MinFlyoutsShown = 10;

        public static bool ShouldPrompt(DateTime? firstRunUtc, DateTime nowUtc, int flyoutsShown, bool alreadyHandled)
        {
            if (alreadyHandled || firstRunUtc is not DateTime firstRun)
                return false;

            return nowUtc - firstRun >= TimeSpan.FromDays(MinDaysSinceFirstRun)
                && flyoutsShown >= MinFlyoutsShown;
        }
    }
}
