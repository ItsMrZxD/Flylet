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

        [Theory]
        [InlineData(0xFF, 0x00, 0x00, 0, 1, 1)]
        [InlineData(0x00, 0xFF, 0x00, 120, 1, 1)]
        [InlineData(0x00, 0x00, 0xFF, 240, 1, 1)]
        [InlineData(0x80, 0x80, 0x80, 0, 0, 0x80 / 255.0)]
        [InlineData(0x00, 0x00, 0x00, 0, 0, 0)]
        public void ColorToHsv_KnownColors(byte r, byte g, byte b, double hue, double saturation, double value)
        {
            var (h, s, v) = AccentColorHelper.ColorToHsv(Color.FromRgb(r, g, b));

            Assert.Equal(hue, h, 3);
            Assert.Equal(saturation, s, 3);
            Assert.Equal(value, v, 3);
        }

        [Theory]
        [InlineData(0xD9, 0x36, 0x36)]
        [InlineData(0x00, 0x78, 0xD7)]
        [InlineData(0xFF, 0xB9, 0x00)]
        [InlineData(0x56, 0x7C, 0x73)]
        [InlineData(0xFF, 0xFF, 0xFF)]
        [InlineData(0x01, 0x02, 0x03)]
        public void Hsv_RoundTripsEveryChannelExactly(byte r, byte g, byte b)
        {
            var color = Color.FromRgb(r, g, b);
            var (h, s, v) = AccentColorHelper.ColorToHsv(color);

            Assert.Equal(color, AccentColorHelper.ColorFromHsv(h, s, v));
        }

        [Theory]
        [InlineData(null, new string[] { null, null, null, null })]
        [InlineData("", new string[] { null, null, null, null })]
        [InlineData("#d93636;;#0078D7", new[] { "#D93636", null, "#0078D7", null })]
        [InlineData("#123;nope;#GGGGGG;#00FF00;#FFFFFF", new[] { null, null, null, "#00FF00" })]
        public void ParseSavedColors_AlwaysGivesFourValidSlots(string value, string[] expected)
        {
            Assert.Equal(expected, AccentColorHelper.ParseSavedColors(value, 4));
        }

        [Fact]
        public void SavedColors_RoundTrip()
        {
            var slots = new[] { "#D93636", null, "#0078d7", "bad" };
            string stored = AccentColorHelper.FormatSavedColors(slots);

            Assert.Equal("#D93636;;#0078D7;", stored);
            Assert.Equal(new[] { "#D93636", null, "#0078D7", null }, AccentColorHelper.ParseSavedColors(stored, 4));
        }

        [Fact]
        public void ColorFromHsv_WrapsHueAndClampsTheRest()
        {
            Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), AccentColorHelper.ColorFromHsv(360, 1, 1));
            Assert.Equal(Color.FromRgb(0x00, 0x00, 0xFF), AccentColorHelper.ColorFromHsv(-120, 2, 5));
        }
    }
}
