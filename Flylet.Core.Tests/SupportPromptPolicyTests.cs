using Flylet.Core.Helpers;
using System;
using Xunit;

namespace Flylet.Core.Tests
{
    public class SupportPromptPolicyTests
    {
        private const int Enough = SupportPromptPolicy.MinFlyoutsThisRun;

        private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime DayAgo = Now.AddDays(-SupportPromptPolicy.MinDaysSinceFirstRun);

        [Fact]
        public void DayPassed_EnoughFlyouts_NothingInTheWay_Shows()
        {
            Assert.True(SupportPromptPolicy.ShouldShow(false, DayAgo, Now, Enough, false, false, false));
        }

        [Fact]
        public void LessThanADaySinceInstall_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, DayAgo.AddMinutes(1), Now, Enough, false, false, false));
        }

        [Fact]
        public void NoFirstRun_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, null, Now, Enough, false, false, false));
        }

        [Fact]
        public void TooFewFlyouts_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, DayAgo, Now, Enough - 1, false, false, false));
        }

        [Fact]
        public void AlreadyHandled_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(true, DayAgo, Now, Enough, false, false, false));
        }

        [Fact]
        public void WhatsNewShownThisRun_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, DayAgo, Now, Enough, true, false, false));
        }

        [Fact]
        public void RatingAttemptedThisRun_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, DayAgo, Now, Enough, false, true, false));
        }

        [Fact]
        public void Fullscreen_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, DayAgo, Now, Enough, false, false, true));
        }
    }
}
