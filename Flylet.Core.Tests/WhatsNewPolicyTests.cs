using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class WhatsNewPolicyTests
    {
        [Fact]
        public void NullLastSeenVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow(null, "1.2.0"));
        }

        [Fact]
        public void EmptyLastSeenVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("", "1.2.0"));
        }

        [Fact]
        public void NewerCurrentVersion_Shows()
        {
            Assert.True(WhatsNewPolicy.ShouldShow("0.11.2", "0.12.0"));
        }

        [Fact]
        public void NumericComparison_ShowsForNewerVersion()
        {
            Assert.True(WhatsNewPolicy.ShouldShow("0.9.0", "0.12.0"));
        }

        [Fact]
        public void SameVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("1.0.0", "1.0.0"));
        }

        [Fact]
        public void OlderCurrentVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("0.12.0", "0.11.2"));
        }

        [Fact]
        public void UnparsableLastSeenVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("abc", "1.2.0"));
        }

        [Fact]
        public void UnparsableCurrentVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("1.2.0", "abc"));
        }

        [Fact]
        public void NullCurrentVersion_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("1.2.0", null));
        }

        [Fact]
        public void NoLastSeenVersion_ButRanBefore_Shows()
        {
            Assert.True(WhatsNewPolicy.ShouldShow("", "0.12.0", hasRunBefore: true));
        }

        [Fact]
        public void NoLastSeenVersion_FreshInstall_DoesNotShow()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("", "0.12.0", hasRunBefore: false));
        }

        [Fact]
        public void SavedLastSeenVersion_WinsOverHavingRunBefore()
        {
            Assert.False(WhatsNewPolicy.ShouldShow("0.12.0", "0.12.0", hasRunBefore: true));
        }
    }
}
