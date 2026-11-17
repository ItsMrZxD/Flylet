using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class BitmapHelperTests
    {
        // A stream filled in small writes, like the album cover streams are: its buffer ends up bigger than the picture
        private static MemoryStream MakeStream(int length)
        {
            var stream = new MemoryStream();
            var chunk = new byte[4096];
            for (int written = 0; written < length; written += chunk.Length)
            {
                stream.Write(chunk, 0, Math.Min(chunk.Length, length - written));
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        [Fact]
        public void ReleaseStream_MemoryStream_GivesItsBufferBackAndClosesIt()
        {
            var stream = MakeStream(600_000);
            Assert.True(stream.Capacity >= 600_000);

            BitmapHelper.ReleaseStream(stream);

            Assert.False(stream.CanRead);
            Assert.Empty(stream.ToArray());
            Assert.True(stream.TryGetBuffer(out var buffer));
            Assert.Empty(buffer.Array);
        }

        [Fact]
        public void ReleaseStream_StreamOverAFixedArray_IsClosedWithoutThrowing()
        {
            var stream = new MemoryStream(new byte[100]);

            BitmapHelper.ReleaseStream(stream);

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void ReleaseStream_ReadOnlyOrClosedOrMissingStream_DoesNotThrow()
        {
            BitmapHelper.ReleaseStream(new MemoryStream(new byte[100], writable: false));

            var closed = MakeStream(1000);
            closed.Dispose();
            BitmapHelper.ReleaseStream(closed);

            BitmapHelper.ReleaseStream(null);
        }

        private static byte[] MakePixels(int width, int height)
        {
            var pixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                pixels[i * 4] = (byte)(i * 3);
                pixels[i * 4 + 1] = (byte)(i / width * 5);
                pixels[i * 4 + 2] = (byte)(255 - i);
                pixels[i * 4 + 3] = 255;
            }

            return pixels;
        }

        private static MemoryStream SavePng(int width, int height)
        {
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, MakePixels(width, height), width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var stream = new MemoryStream();
            encoder.Save(stream);
            return stream;
        }

        [Fact]
        public void TryCreateBitmapImageFromStream_GivesTheSamePictureAsDecodingTheStream()
        {
            using var stream = SavePng(40, 30);
            stream.Seek(0, SeekOrigin.Begin);
            var decoded = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            // Left at the end on purpose: the helper rewinds it
            stream.Seek(0, SeekOrigin.End);
            Assert.True(BitmapHelper.TryCreateBitmapImageFromStream(stream, out var bitmap));

            Assert.True(bitmap.IsFrozen);
            Assert.Equal(decoded.PixelWidth, bitmap.PixelWidth);
            Assert.Equal(decoded.PixelHeight, bitmap.PixelHeight);
            Assert.Equal(decoded.Format, bitmap.Format);
            Assert.Equal(decoded.DpiX, bitmap.DpiX);
            Assert.Equal(decoded.DpiY, bitmap.DpiY);

            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
            var expected = new byte[stride * decoded.PixelHeight];
            var actual = new byte[stride * bitmap.PixelHeight];
            decoded.CopyPixels(expected, stride, 0);
            bitmap.CopyPixels(actual, stride, 0);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TryCreateBitmapImageFromStream_PictureStaysAfterTheStreamIsReleased()
        {
            var stream = SavePng(40, 30);
            Assert.True(BitmapHelper.TryCreateBitmapImageFromStream(stream, out var bitmap));

            BitmapHelper.ReleaseStream(stream);

            var pixels = new byte[40 * 30 * 4];
            bitmap.CopyPixels(pixels, 40 * 4, 0);
            Assert.Contains(pixels, value => value != 0);
        }

        [Fact]
        public void TryCreateBitmapImageFromStream_EmptyOrMissingStream_ReturnsFalse()
        {
            Assert.False(BitmapHelper.TryCreateBitmapImageFromStream(null, out var none));
            Assert.Null(none);
            Assert.False(BitmapHelper.TryCreateBitmapImageFromStream(new MemoryStream(), out var empty));
            Assert.Null(empty);
        }

        // In its own method, so nothing of the decoding is left on the test's stack
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (BitmapSource Bitmap, WeakReference Stream) DecodeAndForgetTheStream()
        {
            var stream = SavePng(40, 30);
            BitmapHelper.TryCreateBitmapImageFromStream(stream, out var bitmap);
            return (bitmap, new WeakReference(stream));
        }

        [Fact]
        public void TryCreateBitmapImageFromStream_PictureDoesNotKeepTheStreamAlive()
        {
            var (bitmap, stream) = DecodeAndForgetTheStream();

            // The decoder lets go of the stream when it is finalized; nothing else may hold it
            for (int i = 0; i < 3 && stream.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            GC.Collect();
            Assert.False(stream.IsAlive);
            GC.KeepAlive(bitmap);
        }
    }
}
