using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class AnimatedBackgroundHelperTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "FlyletTests_" + Guid.NewGuid().ToString("N"));

        public AnimatedBackgroundHelperTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        private string SaveGif(int width, int height, int frames, string name = "test.gif", ushort delayHundredths = 0)
        {
            var encoder = new GifBitmapEncoder();
            for (int i = 0; i < frames; i++)
            {
                var pixels = new byte[width * height * 4];
                for (int p = 0; p < pixels.Length; p += 4)
                {
                    pixels[p] = (byte)(i * 60);
                    pixels[p + 1] = 128;
                    pixels[p + 2] = (byte)(255 - i * 60);
                    pixels[p + 3] = 255;
                }

                var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
            }

            string path = Path.Combine(folder, name);
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            if (delayHundredths > 0)
            {
                // WPF's encoder always writes a 0 delay; set it in each graphic control extension (21 F9 04)
                var bytes = File.ReadAllBytes(path);
                for (int i = 0; i + 5 < bytes.Length; i++)
                {
                    if (bytes[i] == 0x21 && bytes[i + 1] == 0xF9 && bytes[i + 2] == 0x04)
                    {
                        bytes[i + 4] = (byte)(delayHundredths & 0xFF);
                        bytes[i + 5] = (byte)(delayHundredths >> 8);
                    }
                }

                File.WriteAllBytes(path, bytes);
            }

            return path;
        }

        [Fact]
        public void IsAnimatedGif_MoreThanOneFrame_IsTrue()
        {
            Assert.True(AnimatedBackgroundHelper.IsAnimatedGif(SaveGif(40, 30, 3)));
        }

        [Fact]
        public void IsAnimatedGif_SingleFrameOrMissingFile_IsFalse()
        {
            Assert.False(AnimatedBackgroundHelper.IsAnimatedGif(SaveGif(40, 30, 1)));
            Assert.False(AnimatedBackgroundHelper.IsAnimatedGif(Path.Combine(folder, "none.gif")));
        }

        [Fact]
        public void TryLoad_ReturnsComposedFramesWithDelays()
        {
            var animation = AnimatedBackgroundHelper.TryLoad(SaveGif(40, 30, 3));

            Assert.NotNull(animation);
            Assert.Equal(animation.Frames.Count, animation.Delays.Count);
            Assert.True(animation.Frames.Count >= 2);
            Assert.Equal(40, animation.PixelWidth);
            Assert.Equal(30, animation.PixelHeight);
            Assert.All(animation.Frames, f => Assert.True(f.IsFrozen));
            Assert.All(animation.Delays, d => Assert.True(d > TimeSpan.Zero));
        }

        [Fact]
        public void TryLoad_BigPicture_IsShrunkToTheMaxSize()
        {
            var animation = AnimatedBackgroundHelper.TryLoad(SaveGif(960, 480, 2));

            Assert.NotNull(animation);
            Assert.Equal(AnimatedBackgroundHelper.MaxSize, animation.PixelWidth);
            Assert.Equal(AnimatedBackgroundHelper.MaxSize / 2, animation.PixelHeight);
        }

        [Fact]
        public void TryLoad_QuarterTurn_SwapsWidthAndHeight()
        {
            var animation = AnimatedBackgroundHelper.TryLoad(SaveGif(40, 30, 3), 1);

            Assert.NotNull(animation);
            Assert.Equal(30, animation.PixelWidth);
            Assert.Equal(40, animation.PixelHeight);
        }

        [Fact]
        public void Rotate_TurnsEveryFrameAndKeepsTheDelays()
        {
            var animation = AnimatedBackgroundHelper.TryLoad(SaveGif(40, 30, 3));

            var rotated = AnimatedBackgroundHelper.Rotate(animation, false);

            Assert.Equal(animation.Frames.Count, rotated.Frames.Count);
            Assert.Equal(animation.Delays, rotated.Delays);
            Assert.Equal(30, rotated.PixelWidth);
            Assert.Equal(40, rotated.PixelHeight);
        }

        [Fact]
        public void Rotate_MovesPixelsTheRightWay()
        {
            // 2x1 picture: left pixel A, right pixel B
            var a = new byte[] { 1, 1, 1, 255 };
            var b = new byte[] { 2, 2, 2, 255 };
            var pixels = new byte[8];
            a.CopyTo(pixels, 0);
            b.CopyTo(pixels, 4);
            var frame = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Pbgra32, null, pixels, 8);
            var animation = new AnimatedBackground(2, 1, new[] { frame, frame }, new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100) });

            // Clockwise: A ends up on top, B below; counter-clockwise: B on top
            var clockwise = Pixels(AnimatedBackgroundHelper.Rotate(animation, true).Frames[0]);
            var counter = Pixels(AnimatedBackgroundHelper.Rotate(animation, false).Frames[0]);

            Assert.Equal(1, clockwise[0]);
            Assert.Equal(2, clockwise[4]);
            Assert.Equal(2, counter[0]);
            Assert.Equal(1, counter[4]);
        }

        [Fact]
        public void TryLoad_KeepsEachFramesOwnPicture()
        {
            var animation = AnimatedBackgroundHelper.TryLoad(SaveGif(40, 30, 3));

            // The test frames differ in color, so composed frames must differ too
            Assert.NotEqual(Pixels(animation.Frames[0])[0], Pixels(animation.Frames[1])[0]);
        }

        private static byte[] Pixels(BitmapSource frame)
        {
            var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
            return pixels;
        }

        [Fact]
        public void TryLoad_ShortFastGif_StaysAnimatedAtTheCappedSpeed()
        {
            string path = SaveGif(40, 30, 2, "fast.gif", delayHundredths: 3);

            // The test file really asks for 30 ms frames
            using (var stream = File.OpenRead(path))
            {
                var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal((ushort)3, ((BitmapMetadata)decoder.Frames[0].Metadata).GetQuery("/grctlext/Delay"));
            }

            var animation = AnimatedBackgroundHelper.TryLoad(path);

            Assert.NotNull(animation);
            Assert.Equal(2, animation.Frames.Count);
            Assert.All(animation.Delays, d => Assert.True(d >= AnimatedBackgroundHelper.MinFrameDelay));
        }

        [Fact]
        public void TryLoad_NotAGif_ReturnsNull()
        {
            string path = Path.Combine(folder, "text.gif");
            File.WriteAllText(path, "not a picture");

            Assert.Null(AnimatedBackgroundHelper.TryLoad(path));
        }
    }
}
