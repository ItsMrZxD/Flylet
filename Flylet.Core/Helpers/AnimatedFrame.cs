using System;
using System.Runtime.InteropServices;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// One composed GIF frame, kept as small as it can be without changing a pixel.
    /// </summary>
    /// <remarks>
    /// A GIF frame rarely has more than 256 colors, so most frames are kept as one byte per pixel plus the
    /// colors those bytes stand for: a quarter of the four bytes per pixel the picture is drawn from.
    /// A frame with more colors (frames with their own palettes drawn over each other, or a GIF that was
    /// shrunk, which blends colors) keeps its pixels as they are.
    /// </remarks>
    public sealed class AnimatedFrame
    {
        private const int MaxColors = 256;

        // Indexed: one byte per pixel, each the place of its color in the palette.
        // Not indexed: the Pbgra32 pixels themselves, four bytes each.
        private readonly byte[] data;
        private readonly uint[] palette;

        private AnimatedFrame(byte[] data, uint[] palette)
        {
            this.data = data;
            this.palette = palette;
        }

        /// <summary>
        /// Whether the frame is kept as one byte per pixel.
        /// </summary>
        public bool IsIndexed => palette != null;

        public int PixelCount => IsIndexed ? data.Length : data.Length / 4;

        /// <summary>
        /// The memory the frame's pixels take, in bytes.
        /// </summary>
        public long StoredBytes => IsIndexed ? data.Length + (long)palette.Length * sizeof(uint) : data.Length;

        /// <summary>
        /// Keeps Pbgra32 pixels (four bytes each) the smallest way that loses nothing. The array isn't copied
        /// when the frame has too many colors, so don't change it afterwards.
        /// </summary>
        public static AnimatedFrame Pack(byte[] pixels)
        {
            ArgumentNullException.ThrowIfNull(pixels);
            if (pixels.Length % 4 != 0)
                throw new ArgumentException("Pixels must be four bytes each.", nameof(pixels));

            var colors = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());

            // Finds each color's place with a small hash table: 1024 slots for at most 256 colors
            Span<uint> found = stackalloc uint[MaxColors];
            Span<short> slots = stackalloc short[1024];
            slots.Fill(-1);
            int count = 0;

            // First only count the colors, so a frame with too many costs nothing more than this look
            uint last = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                uint color = colors[i];
                if (i > 0 && color == last)
                    continue;

                last = color;
                if (FindOrAdd(color, found, slots, ref count) < 0)
                    return new AnimatedFrame(pixels, null);
            }

            // No gain for a picture of a few pixels
            if (colors.Length + count * sizeof(uint) >= pixels.Length)
                return new AnimatedFrame(pixels, null);

            var indexes = new byte[colors.Length];
            byte lastIndex = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                uint color = colors[i];
                if (i == 0 || color != last)
                {
                    last = color;
                    lastIndex = (byte)FindOrAdd(color, found, slots, ref count);
                }

                indexes[i] = lastIndex;
            }

            return new AnimatedFrame(indexes, found[..count].ToArray());
        }

        /// <summary>
        /// The color's place in <paramref name="found"/>, adding it when it's new; -1 when there's no room left.
        /// </summary>
        private static int FindOrAdd(uint color, Span<uint> found, Span<short> slots, ref int count)
        {
            int slot = (int)((color * 2654435761u) >> 22);
            while (true)
            {
                int place = slots[slot];
                if (place < 0)
                {
                    if (count == MaxColors)
                        return -1;

                    found[count] = color;
                    slots[slot] = (short)count;
                    return count++;
                }

                if (found[place] == color)
                    return place;

                slot = (slot + 1) & (slots.Length - 1);
            }
        }

        /// <summary>
        /// Writes the frame's Pbgra32 pixels, four bytes each, into <paramref name="destination"/>.
        /// </summary>
        public void CopyPixelsTo(Span<byte> destination)
        {
            if (destination.Length < PixelCount * 4)
                throw new ArgumentException("Too small for the frame.", nameof(destination));

            if (!IsIndexed)
            {
                data.CopyTo(destination);
                return;
            }

            var target = MemoryMarshal.Cast<byte, uint>(destination);
            var colors = palette;
            for (int i = 0; i < data.Length; i++)
            {
                target[i] = colors[data[i]];
            }
        }

        /// <summary>
        /// A new array with the frame's Pbgra32 pixels.
        /// </summary>
        public byte[] ToPixels()
        {
            var pixels = new byte[PixelCount * 4];
            CopyPixelsTo(pixels);
            return pixels;
        }

        /// <summary>
        /// The frame's own pixel array when it isn't indexed, so it can be drawn from without a copy; else null.
        /// </summary>
        internal byte[] UnpackedPixels => IsIndexed ? null : data;

        /// <summary>
        /// The frame turned a quarter turn. The frame is <paramref name="width"/> by <paramref name="height"/>
        /// pixels; the turned one is height wide and width tall.
        /// </summary>
        public AnimatedFrame Rotate(int width, int height, bool clockwise)
        {
            if (width <= 0 || height <= 0 || width * height != PixelCount)
                throw new ArgumentException("The size doesn't match the frame.");

            var rotated = new byte[data.Length];
            if (IsIndexed)
            {
                Turn<byte>(data, rotated, width, height, clockwise);
            }
            else
            {
                Turn(MemoryMarshal.Cast<byte, uint>(data.AsSpan()), MemoryMarshal.Cast<byte, uint>(rotated.AsSpan()), width, height, clockwise);
            }

            return new AnimatedFrame(rotated, palette);
        }

        private static void Turn<T>(ReadOnlySpan<T> source, Span<T> target, int width, int height, bool clockwise)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int newX = clockwise ? height - 1 - y : y;
                    int newY = clockwise ? x : width - 1 - x;
                    target[newY * height + newX] = source[y * width + x];
                }
            }
        }
    }
}
