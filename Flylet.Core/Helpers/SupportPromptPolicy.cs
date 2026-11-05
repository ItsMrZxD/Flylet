namespace Flylet.Core.Helpers
{
    /// <summary>
    /// When to show the support card: once ever, after a little use in this run, and never next to another prompt or over a fullscreen app.
    /// </summary>
    public static class SupportPromptPolicy
    {
        public const int MinFlyoutsThisRun = 3;

        public static bool ShouldShow(bool alreadyHandled, int flyoutsThisRun, bool whatsNewShowing, bool ratingAttemptedThisRun, bool fullscreen)
        {
            return !alreadyHandled
                && flyoutsThisRun >= MinFlyoutsThisRun
                && !whatsNewShowing
                && !ratingAttemptedThisRun
                && !fullscreen;
        }
    }
}
