using Flylet.Core.Media;
using Xunit;

namespace Flylet.Core.Tests
{
    public class VisualizerStyleTests
    {
        [Fact]
        public void Presets_AreDifferent_AndGrowFromSubtleToParty()
        {
            Assert.NotEqual(VisualizerStyle.Subtle, VisualizerStyle.Lively);
            Assert.NotEqual(VisualizerStyle.Lively, VisualizerStyle.Party);
            Assert.True(VisualizerStyle.Subtle.Amount < VisualizerStyle.Lively.Amount);
            Assert.True(VisualizerStyle.Lively.Amount < VisualizerStyle.Party.Amount);
            Assert.Equal(VisualizerExtras.None, VisualizerStyle.Subtle.Extras);
        }

        [Fact]
        public void FromPreset_CustomUsesTheUsersMix()
        {
            var custom = new VisualizerStyle(VisualizerExtras.Sparkles | VisualizerExtras.PastThumb, 90);

            Assert.Equal(custom, VisualizerStyle.FromPreset(VisualizerPreset.Custom, custom));
            Assert.Equal(VisualizerStyle.Party, VisualizerStyle.FromPreset(VisualizerPreset.Party, custom));
        }

        [Fact]
        public void With_TurnsOneExtraOnAndOff()
        {
            var style = VisualizerStyle.Subtle.With(VisualizerExtras.Glow, true);
            Assert.True(style.Has(VisualizerExtras.Glow));
            Assert.False(style.Has(VisualizerExtras.Fill));

            style = style.With(VisualizerExtras.Glow, false);
            Assert.False(style.Has(VisualizerExtras.Glow));
            Assert.False(style.Has(VisualizerExtras.None));
        }

        [Theory]
        [InlineData(0, VisualizerStyle.MinAmount)]
        [InlineData(100, 100)]
        [InlineData(999, VisualizerStyle.MaxAmount)]
        public void Normalized_KeepsAmountInRange(int amount, int expected)
        {
            Assert.Equal(expected, new VisualizerStyle(VisualizerExtras.None, amount).Normalized().Amount);
        }

        [Fact]
        public void Normalized_DropsUnknownExtras()
        {
            var style = new VisualizerStyle((VisualizerExtras)(1 | 1024), 100).Normalized();
            Assert.Equal(VisualizerExtras.Fill, style.Extras);
        }

        [Fact]
        public void FakeMusic_StaysInRange_AndRepeats()
        {
            for (double t = 0; t < 30; t += 0.037)
            {
                var (bass, mid, high) = FakeMusic.Levels(t);
                Assert.InRange(bass, 0f, 1f);
                Assert.InRange(mid, 0f, 1f);
                Assert.InRange(high, 0f, 1f);
            }
            Assert.Equal(FakeMusic.Levels(12.5), FakeMusic.Levels(12.5));
        }
    }
}
