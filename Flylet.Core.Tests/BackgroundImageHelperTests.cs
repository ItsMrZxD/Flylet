using System;
using System.IO;
using System.Windows;
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

        [Fact]
        public void GetViewbox_TallPhotoInWideTile_ShowsAThinSliceAtThePickedHeight()
        {
            // 1:2 photo in a 4:1 tile: full width, an eighth of the height
            var top = BackgroundImageHelper.GetViewbox(0.5, 4, 1, 0.5, 0);
            var bottom = BackgroundImageHelper.GetViewbox(0.5, 4, 1, 0.5, 1);

            Assert.Equal(1, top.Width, 6);
            Assert.Equal(0.125, top.Height, 6);
            Assert.Equal(0, top.Y, 6);
            Assert.Equal(0.875, bottom.Y, 6);
        }

        [Fact]
        public void GetViewbox_ZoomShrinksTheViewAndKeepsItInsideThePhoto()
        {
            var view = BackgroundImageHelper.GetViewbox(2, 1, 2, 1, 1);

            Assert.Equal(0.25, view.Width, 6);
            Assert.Equal(0.5, view.Height, 6);
            Assert.Equal(1, view.Right, 6);
            Assert.Equal(1, view.Bottom, 6);
        }

        // Two cards, 360 wide, 50 and 92 tall with 8 between them, painted 70 past their edges for the blur
        private static readonly Size Stack = new(360, 150);

        private static Rect PaintArea(double cardTop, double cardHeight) => new(-70, cardTop - 70, 360 + 140, cardHeight + 140);

        [Fact]
        public void GetPhotoViewport_NoZoom_PhotosFullWidthLinesUpWithTheCards()
        {
            // A 3:2 photo in a much wider flyout: all of its width has to show, none lost to the blur margin
            var view = BackgroundImageHelper.GetViewbox(1.5, Stack.Width / Stack.Height, 1, 0.5, 0.5);
            var paint = PaintArea(0, 50);

            var viewport = BackgroundImageHelper.GetPhotoViewport(view, Stack, paint);

            Assert.Equal(0, paint.X + viewport.X * paint.Width, 6);
            Assert.Equal(360, paint.X + viewport.Right * paint.Width, 6);
        }

        [Fact]
        public void GetPhotoViewport_PannedAllTheWay_ReachesThePhotosEdge()
        {
            var view = BackgroundImageHelper.GetViewbox(1.5, Stack.Width / Stack.Height, 2, 1, 1);
            var paint = PaintArea(0, 50);

            var viewport = BackgroundImageHelper.GetPhotoViewport(view, Stack, paint);

            Assert.Equal(360, paint.X + viewport.Right * paint.Width, 6);
        }

        [Fact]
        public void GetPhotoViewport_EveryCardPutsThePhotoInTheSamePlace()
        {
            // The picture runs on from one card into the next, so it can't jump between them
            var view = BackgroundImageHelper.GetViewbox(1.5, Stack.Width / Stack.Height, 1.5, 0.3, 0.7);
            var top = PaintArea(0, 50);
            var bottom = PaintArea(58, 92);

            var topViewport = BackgroundImageHelper.GetPhotoViewport(view, Stack, top);
            var bottomViewport = BackgroundImageHelper.GetPhotoViewport(view, Stack, bottom);

            Assert.Equal(top.Y + topViewport.Y * top.Height, bottom.Y + bottomViewport.Y * bottom.Height, 6);
            Assert.Equal(topViewport.Height * top.Height, bottomViewport.Height * bottom.Height, 6);
            Assert.Equal(topViewport.X, bottomViewport.X, 6);
        }

        [Fact]
        public void TryRotate_SwapsWidthAndHeight()
        {
            string source = SavePng(300, 100);
            string stored = Path.Combine(folder, "stored.jpg");
            Assert.True(BackgroundImageHelper.TryImport(source, stored));

            Assert.True(BackgroundImageHelper.TryRotate(stored, true));

            var rotated = BackgroundImageHelper.TryLoad(stored);
            Assert.Equal(100, rotated.Width, 0);
            Assert.Equal(300, rotated.Height, 0);
        }

        [Fact]
        public void TryRotate_MissingFile_ReturnsFalse()
        {
            Assert.False(BackgroundImageHelper.TryRotate(Path.Combine(folder, "none.jpg"), false));
        }

        [Fact]
        public void GetViewbox_KeepsTheTileShape()
        {
            var view = BackgroundImageHelper.GetViewbox(1.5, 3.6, 1.7, 0.3, 0.8);

            Assert.Equal(3.6 / 1.5, view.Width / view.Height, 6);
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
