using System;
using System.Collections.Generic;
using System.Windows.Media;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class AccentColorHelperTests
    {
        private static byte[] Pixels(params (byte R, byte G, byte B, byte A, int Count)[] groups)
        {
            var pixels = new List<byte>();
            foreach (var (r, g, b, a, count) in groups)
            {
                for (int i = 0; i < count; i++)
                {
                    pixels.AddRange(new[] { b, g, r, a });
                }
            }
            return pixels.ToArray();
        }

        private static double Lightness((byte R, byte G, byte B) color) =>
            AccentColorHelper.RgbToHsl(color.R / 255.0, color.G / 255.0, color.B / 255.0).Lightness;

        [Fact]
        public void SaturatedRed_GivesSoftRed()
        {
            var accent = AccentColorHelper.PickAccent(Pixels((220, 20, 20, 255, 100)));

            Assert.NotNull(accent);
            var color = accent.Value;
            Assert.True(color.R > color.G + 50);
            Assert.InRange(Math.Abs(color.G - color.B), 0, 2);
            Assert.InRange(Lightness(color), AccentColorHelper.MinLightness - 0.01, AccentColorHelper.MaxLightness + 0.01);
        }

        [Fact]
        public void DarkColor_IsLightened()
        {
            var accent = AccentColorHelper.PickAccent(Pixels((20, 60, 20, 255, 100)));

            Assert.NotNull(accent);
            Assert.True(accent.Value.G > accent.Value.R);
            Assert.True(Lightness(accent.Value) >= AccentColorHelper.MinLightness - 0.01);
        }

        [Fact]
        public void MostCommonHue_Wins()
        {
            var accent = AccentColorHelper.PickAccent(Pixels((20, 20, 220, 255, 70), (220, 20, 20, 255, 30)));

            Assert.NotNull(accent);
            Assert.True(accent.Value.B > accent.Value.R);
        }

        [Fact]
        public void GrayImage_HasNoAccent()
        {
            Assert.Null(AccentColorHelper.PickAccent(Pixels((128, 128, 128, 255, 100))));
        }

        [Fact]
        public void TransparentImage_HasNoAccent()
        {
            Assert.Null(AccentColorHelper.PickAccent(Pixels((220, 20, 20, 0, 100))));
        }

        [Fact]
        public void FewColorfulPixels_HaveNoAccent()
        {
            Assert.Null(AccentColorHelper.PickAccent(Pixels((220, 20, 20, 255, 2), (128, 128, 128, 255, 98))));
        }

        [Fact]
        public void Shade_LightensAndDarkens()
        {
            var color = Color.FromRgb(200, 100, 100);

            var lighter = AccentColorHelper.Shade(color, 0.1);
            var darker = AccentColorHelper.Shade(color, -0.1);

            Assert.True(lighter.R + lighter.G + lighter.B > color.R + color.G + color.B);
            Assert.True(darker.R + darker.G + darker.B < color.R + color.G + color.B);
        }

        [Theory]
        [InlineData("#0078D7", 0xFF, 0x00, 0x78, 0xD7)]
        [InlineData("#800078D7", 0x80, 0x00, 0x78, 0xD7)]
        [InlineData("Red", 0xFF, 0xFF, 0x00, 0x00)]
        public void TryParseColor_ReadsValidColors(string value, byte a, byte r, byte g, byte b)
        {
            Assert.True(AccentColorHelper.TryParseColor(value, out var color));
            Assert.Equal(Color.FromArgb(a, r, g, b), color);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("##0078D7")]
        [InlineData("#GGGGGG")]
        [InlineData("not a color")]
        [InlineData("#")]
        [InlineData("#12345")]
        [InlineData("#1234567890")]
        [InlineData("sc#a,b,c")]
        [InlineData("1234")]
        public void TryParseColor_RejectsBadInputWithoutThrowing(string value)
        {
            Assert.False(AccentColorHelper.TryParseColor(value, out _));
        }

        [Theory]
        [InlineData("#FFFFFF", true)]
        [InlineData("#EFEFEF", true)]  // default light flyout background
        [InlineData("#FFB900", true)]  // gold swatch
        [InlineData("#00B7C3", true)]  // teal swatch
        [InlineData("#000000", false)]
        [InlineData("#222222", false)] // default dark flyout background
        [InlineData("#0B6A0B", false)] // green swatch
        [InlineData("#3F3F3F", false)]
        public void IsLight_FollowsContrast(string hex, bool expected)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);

            Assert.Equal(expected, AccentColorHelper.IsLight(color));
        }
    }
}
