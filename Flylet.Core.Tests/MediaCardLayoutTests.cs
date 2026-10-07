using Flylet.Core.Media;
using Xunit;

namespace Flylet.Core.Tests
{
    public class MediaCardLayoutTests
    {
        [Fact]
        public void Classic_KeepsTheOriginalCardHeight()
        {
            // 178 is the height the card had before it could be customized
            Assert.Equal(178, MediaCardLayout.Classic.Height);
        }

        [Theory]
        [InlineData(MediaCardPreset.Classic)]
        [InlineData(MediaCardPreset.Minimal)]
        [InlineData(MediaCardPreset.CompactPill)]
        [InlineData(MediaCardPreset.AlbumBackground)]
        public void FromPreset_RoundTripsThroughPreset(MediaCardPreset preset)
        {
            Assert.Equal(preset, MediaCardLayout.FromPreset(preset).Preset);
        }

        [Fact]
        public void ChangingOneOption_MakesItCustom()
        {
            var layout = MediaCardLayout.Classic with { ShowArtist = false };

            Assert.Equal(MediaCardPreset.Custom, layout.Preset);
        }

        [Fact]
        public void Custom_FallsBackToClassic()
        {
            Assert.Equal(MediaCardLayout.Classic, MediaCardLayout.FromPreset(MediaCardPreset.Custom));
        }

        [Fact]
        public void Pill_IsAlwaysPillHeight()
        {
            var layout = MediaCardLayout.Classic with { Shape = MediaCardShape.Pill };

            Assert.Equal(MediaCardLayout.PillHeight, layout.Height);
            Assert.Equal(MediaCardLayout.PillHeight / 2, layout.CornerRadius);
        }

        [Fact]
        public void Pill_HidesSourceAndShrinksFullTimeline()
        {
            var layout = MediaCardLayout.Classic with { Shape = MediaCardShape.Pill };

            Assert.False(layout.SourceVisible);
            Assert.False(layout.ControlsBelowText);
            Assert.Equal(MediaCardTimeline.Thin, layout.EffectiveTimeline);
        }

        [Fact]
        public void Minimal_IsJustTheTextAndPadding()
        {
            var expected = MediaCardLayout.Padding
                + MediaCardLayout.TitleRowHeight + MediaCardLayout.ArtistRowHeight
                + MediaCardLayout.Padding;

            Assert.Equal(expected, MediaCardLayout.Minimal.Height);
        }

        [Fact]
        public void PlayOnlyControls_NeverMakeTheTextBlockShorterThanTheButton()
        {
            var layout = MediaCardLayout.Minimal with { ShowArtist = false, Controls = MediaCardControls.PlayOnly };

            Assert.Equal(MediaCardLayout.ButtonSize, layout.TopSectionHeight);
        }

        [Fact]
        public void ArtTile_KeepsTheTopSectionAtLeastArtSize()
        {
            var layout = MediaCardLayout.Classic with { ShowSource = false, ShowArtist = false, Controls = MediaCardControls.Hidden };

            Assert.Equal(MediaCardLayout.ArtSize, layout.TopSectionHeight);
        }

        [Fact]
        public void Square_HasNoCornerRadius()
        {
            Assert.Equal(0, (MediaCardLayout.Classic with { Shape = MediaCardShape.Square }).CornerRadius);
        }
    }
}
