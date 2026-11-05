using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class SupportPromptPolicyTests
    {
        private const int Enough = SupportPromptPolicy.MinFlyoutsThisRun;

        [Fact]
        public void EnoughFlyouts_NothingInTheWay_Shows()
        {
            Assert.True(SupportPromptPolicy.ShouldShow(false, Enough, false, false, false));
        }

        [Fact]
        public void TooFewFlyouts_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, Enough - 1, false, false, false));
        }

        [Fact]
        public void AlreadyHandled_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(true, Enough, false, false, false));
        }

        [Fact]
        public void WhatsNewShowing_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, Enough, true, false, false));
        }

        [Fact]
        public void RatingAttemptedThisRun_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, Enough, false, true, false));
        }

        [Fact]
        public void Fullscreen_DoesNotShow()
        {
            Assert.False(SupportPromptPolicy.ShouldShow(false, Enough, false, false, true));
        }
    }
}
