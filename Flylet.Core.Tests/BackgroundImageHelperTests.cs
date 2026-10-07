using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class BackgroundImageHelperTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "FlyletTests_" + Guid.NewGuid().ToString("N"));

        public BackgroundImageHelperTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        private string SavePng(int width, int height, string name = "source.png")
        {
            var pixels = new byte[width * height * 4];
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string path = Path.Combine(folder, name);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return path;
        }

        private static BitmapSource Decode(string path)
        {
            using var stream = File.OpenRead(path);
            return BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        }

        [Fact]
        public void LargePicture_IsShrunkToMaxSize()
        {
            string destination = Path.Combine(folder, "out.jpg");

            Assert.True(BackgroundImageHelper.TryImport(SavePng(2000, 1000), destination));

            var result = Decode(destination);
            Assert.Equal(BackgroundImageHelper.MaxSize, result.PixelWidth);
            Assert.Equal(BackgroundImageHelper.MaxSize / 2, result.PixelHeight);
        }

        [Fact]
        public void SmallPicture_KeepsItsSize()
        {
            string destination = Path.Combine(folder, "out.jpg");

            Assert.True(BackgroundImageHelper.TryImport(SavePng(300, 200), destination));

            var result = Decode(destination);
            Assert.Equal(300, result.PixelWidth);
            Assert.Equal(200, result.PixelHeight);
        }

        [Fact]
        public void NotAPicture_FailsAndLeavesTheOldCopy()
        {
            string destination = Path.Combine(folder, "out.jpg");
            Assert.True(BackgroundImageHelper.TryImport(SavePng(300, 200), destination));
            string text = Path.Combine(folder, "notes.txt");
            File.WriteAllText(text, "not a picture");

            Assert.False(BackgroundImageHelper.TryImport(text, destination));

            Assert.Equal(300, Decode(destination).PixelWidth);
            Assert.False(File.Exists(destination + ".tmp"));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(8)]
        public void SidewaysOrientation_SwapsWidthAndHeight(int orientation)
        {
            var image = BitmapSource.Create(40, 10, 96, 96, PixelFormats.Bgra32, null, new byte[40 * 10 * 4], 40 * 4);

            var upright = BackgroundImageHelper.ApplyExifOrientation(image, orientation);

            Assert.Equal(10, upright.PixelWidth);
            Assert.Equal(40, upright.PixelHeight);
        }

        [Fact]
        public void TryLoad_MissingFile_ReturnsNull()
        {
            Assert.Null(BackgroundImageHelper.TryLoad(Path.Combine(folder, "missing.jpg")));
        }
    }
}
