using System;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class AnimatedFrameTests
    {
        // Pbgra32 pixels, four bytes each, cycling through the given number of colors
        private static byte[] MakePixels(int width, int height, int colors)
        {
            var pixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int color = (i * 7 + i / width) % colors;
                pixels[i * 4] = (byte)color;
                pixels[i * 4 + 1] = (byte)(color >> 8);
                pixels[i * 4 + 2] = (byte)(255 - (color & 0xFF));
                pixels[i * 4 + 3] = 255;
            }

            return pixels;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(200)]
        [InlineData(256)]
        public void Pack_UpTo256Colors_IsIndexedAndGivesTheSamePixelsBack(int colors)
        {
            var pixels = MakePixels(64, 48, colors);

            var frame = AnimatedFrame.Pack((byte[])pixels.Clone());

            Assert.True(frame.IsIndexed);
            Assert.Equal(64 * 48, frame.PixelCount);
            Assert.Equal(64 * 48 + colors * 4, frame.StoredBytes);
            Assert.Equal(pixels, frame.ToPixels());
        }

        [Theory]
        [InlineData(257)]
        [InlineData(3000)]
        public void Pack_MoreThan256Colors_KeepsThePixelsAsTheyAre(int colors)
        {
            var pixels = MakePixels(64, 48, colors);

            var frame = AnimatedFrame.Pack(pixels);

            Assert.False(frame.IsIndexed);
            Assert.Equal(pixels.Length, frame.StoredBytes);
            Assert.Equal(pixels, frame.ToPixels());
        }

        [Fact]
        public void Pack_KeepsTransparentAndHalfTransparentPixels()
        {
            // Premultiplied: fully clear, half clear, solid
            var pixels = new byte[400 * 4];
            for (int i = 0; i < 400; i++)
            {
                byte alpha = (byte)(i % 3 == 0 ? 0 : i % 3 == 1 ? 128 : 255);
                pixels[i * 4] = (byte)(alpha / 2);
                pixels[i * 4 + 1] = (byte)(alpha / 4);
                pixels[i * 4 + 2] = alpha;
                pixels[i * 4 + 3] = alpha;
            }

            var frame = AnimatedFrame.Pack((byte[])pixels.Clone());

            Assert.True(frame.IsIndexed);
            Assert.Equal(pixels, frame.ToPixels());
        }

        [Fact]
        public void Pack_ColorsThatShareAHashSlot_StayApart()
        {
            // 256 colors that differ only in their top byte, then only in their bottom byte
            foreach (int shift in new[] { 0, 8, 16, 24 })
            {
                var pixels = new byte[1024 * 4];
                for (int i = 0; i < 1024; i++)
                {
                    BitConverter.TryWriteBytes(pixels.AsSpan(i * 4), (uint)(i % 256) << shift);
                }

                var frame = AnimatedFrame.Pack((byte[])pixels.Clone());

                Assert.True(frame.IsIndexed);
                Assert.Equal(pixels, frame.ToPixels());
            }
        }

        [Fact]
        public void Pack_PictureOfAFewPixels_IsNotIndexed()
        {
            var frame = AnimatedFrame.Pack(MakePixels(1, 1, 1));

            Assert.False(frame.IsIndexed);
            Assert.Equal(1, frame.PixelCount);
        }

        [Fact]
        public void Pack_PixelsNotFourBytesEach_Throws()
        {
            Assert.Throws<ArgumentException>(() => AnimatedFrame.Pack(new byte[10]));
        }

        [Fact]
        public void CopyPixelsTo_TooSmallABuffer_Throws()
        {
            var frame = AnimatedFrame.Pack(MakePixels(8, 8, 4));

            Assert.Throws<ArgumentException>(() => frame.CopyPixelsTo(new byte[8 * 8 * 4 - 1]));
        }

        [Theory]
        [InlineData(16, true)]
        [InlineData(16, false)]
        [InlineData(3000, true)]
        [InlineData(3000, false)]
        public void Rotate_GivesTheSamePictureAsTurningThePixels(int colors, bool clockwise)
        {
            const int width = 40, height = 30;
            var pixels = MakePixels(width, height, colors);
            var frame = AnimatedFrame.Pack((byte[])pixels.Clone());

            var rotated = frame.Rotate(width, height, clockwise);

            // The turned picture is height wide and width tall
            var expected = new byte[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int newX = clockwise ? height - 1 - y : y;
                    int newY = clockwise ? x : width - 1 - x;
                    Array.Copy(pixels, (y * width + x) * 4, expected, (newY * height + newX) * 4, 4);
                }
            }

            Assert.Equal(frame.IsIndexed, rotated.IsIndexed);
            Assert.Equal(expected, rotated.ToPixels());
        }

        [Fact]
        public void Rotate_FourTimes_IsTheSamePictureAgain()
        {
            var pixels = MakePixels(40, 30, 100);
            var frame = AnimatedFrame.Pack((byte[])pixels.Clone());

            var turned = frame.Rotate(40, 30, true).Rotate(30, 40, true).Rotate(40, 30, true).Rotate(30, 40, true);

            Assert.Equal(pixels, turned.ToPixels());
        }

        [Fact]
        public void Rotate_WrongSize_Throws()
        {
            var frame = AnimatedFrame.Pack(MakePixels(40, 30, 16));

            Assert.Throws<ArgumentException>(() => frame.Rotate(30, 30, true));
        }
    }
}
