using System;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class RatePromptPolicyTests
    {
        private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ThreeDaysAndTenFlyouts_Prompts()
        {
            Assert.True(RatePromptPolicy.ShouldPrompt(Now.AddDays(-3), Now, 10, false));
        }

        [Fact]
        public void TooFewDays_DoesNotPrompt()
        {
            Assert.False(RatePromptPolicy.ShouldPrompt(Now.AddDays(-2), Now, 500, false));
        }

        [Fact]
        public void TooFewFlyouts_DoesNotPrompt()
        {
            Assert.False(RatePromptPolicy.ShouldPrompt(Now.AddDays(-30), Now, 9, false));
        }

        [Fact]
        public void AlreadyShownOrDismissed_NeverPromptsAgain()
        {
            Assert.False(RatePromptPolicy.ShouldPrompt(Now.AddDays(-30), Now, 500, true));
        }

        [Fact]
        public void UnknownFirstRun_DoesNotPrompt()
        {
            Assert.False(RatePromptPolicy.ShouldPrompt(null, Now, 500, false));
        }
    }
}
