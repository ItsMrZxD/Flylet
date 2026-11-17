using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Helpers
{
    public class BitmapHelper
    {
        /// <summary>
        /// Decodes the picture in <paramref name="stream"/> into a frozen bitmap with its own copy of the
        /// pixels. Nothing of the stream is needed afterwards: give its memory back with <see cref="ReleaseStream"/>.
        /// </summary>
        public static bool TryCreateBitmapImageFromStream(Stream stream, out BitmapSource bitmap)
        {
            bitmap = null;

            if (stream != null && stream.Length > 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                bitmap = CopyOf(frame);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The same picture without its decoder. A decoded frame keeps its decoder for as long as it lives,
        /// and the decoder keeps the stream it read from, even with BitmapCacheOption.OnLoad: every album
        /// cover held on to its whole file that way (often 1 MB), until some later garbage collection.
        /// </summary>
        private static BitmapSource CopyOf(BitmapSource source)
        {
            int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
            long size = (long)stride * source.PixelHeight;
            if (size <= 0 || size > int.MaxValue)
                return source;

            IntPtr pixels = Marshal.AllocHGlobal((IntPtr)size);
            try
            {
                source.CopyPixels(Int32Rect.Empty, pixels, (int)size, stride);
                var copy = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY,
                    source.Format, source.Palette, pixels, (int)size, stride);
                copy.Freeze();
                return copy;
            }
            finally
            {
                Marshal.FreeHGlobal(pixels);
            }
        }

        /// <summary>
        /// Closes a stream whose picture has been decoded and gives its memory back right away. Closing a
        /// MemoryStream alone keeps its buffer for as long as anything still refers to the stream, and a
        /// decoder that has read from it does until it is garbage collected.
        /// </summary>
        public static void ReleaseStream(Stream stream)
        {
            if (stream == null)
                return;

            if (stream is MemoryStream memory)
            {
                try
                {
                    memory.SetLength(0);
                    memory.Capacity = 0;
                }
                catch (Exception e) when (e is NotSupportedException or ObjectDisposedException)
                {
                    // Made over a fixed array, or closed already
                }
            }

            stream.Dispose();
        }
    }
}
