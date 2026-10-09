using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// A GIF turned into ready-to-show frames: shrunk, fully composed (GIF frames are often only the
    /// changed part), and thinned out so a long GIF can't use up the PC's memory.
    /// </summary>
    /// <remarks>
    /// Each frame is its own frozen bitmap, swapped in on every tick. Writing new pixels into one shared
    /// WriteableBitmap was tried: WPF didn't redraw the flyout cards for it, so the GIF stood still.
    /// </remarks>
    public sealed class AnimatedBackground
    {
        public AnimatedBackground(int pixelWidth, int pixelHeight, IReadOnlyList<BitmapSource> frames, IReadOnlyList<TimeSpan> delays)
        {
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            Frames = frames;
            Delays = delays;
        }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public IReadOnlyList<BitmapSource> Frames { get; }

        /// <summary>
        /// How long each frame is shown before the next one.
        /// </summary>
        public IReadOnlyList<TimeSpan> Delays { get; }
    }

    public static class AnimatedBackgroundHelper
    {
        /// <summary>
        /// Biggest GIF accepted. Decoding takes a few seconds near this size; the frames kept in memory
        /// are limited separately by <see cref="MaxFrameMemory"/>.
        /// </summary>
        public const long MaxFileSize = 50L * 1024 * 1024;

        /// <summary>
        /// Longest side of a frame; flyouts are 360 px wide and the photo is blurred.
        /// </summary>
        public const int MaxSize = 480;

        /// <summary>
        /// Memory for all kept frames together. Over this, frames are dropped (the delay goes to the frame before).
        /// </summary>
        public const long MaxFrameMemory = 96L * 1024 * 1024;

        /// <summary>
        /// Shortest time a frame stays. 20 frames a second is smooth enough behind blur and light on the PC.
        /// </summary>
        public static readonly TimeSpan MinFrameDelay = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Delay for GIFs that ask for no delay at all; browsers treat those as 100 ms too.
        /// </summary>
        private static readonly TimeSpan DefaultFrameDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// True if the file is a GIF with more than one frame.
        /// </summary>
        public static bool IsAnimatedGif(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return decoder is GifBitmapDecoder && decoder.Frames.Count > 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the GIF at <paramref name="path"/> into frames, turned <paramref name="quarterTurns"/> times
        /// clockwise. Safe to call from a background thread.
        /// </summary>
        /// <returns>Null if it isn't a readable GIF, is too big, or has only one frame.</returns>
        public static AnimatedBackground TryLoad(string path, int quarterTurns = 0)
        {
            try
            {
                if (new FileInfo(path).Length > MaxFileSize)
                    return null;

                // Kept open while the frames are read, so they're decoded one at a time instead of all up front
                using var stream = File.OpenRead(path);
                var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

                int count = decoder.Frames.Count;
                if (count < 2)
                    return null;

                var (screenWidth, screenHeight) = GetScreenSize(decoder);
                double scale = Math.Min(1.0, (double)MaxSize / Math.Max(screenWidth, screenHeight));
                int width = Math.Max(1, (int)Math.Round(screenWidth * scale));
                int height = Math.Max(1, (int)Math.Round(screenHeight * scale));
                int stride = width * 4;
                var everything = new Rect(0, 0, width, height);

                long frameBytes = (long)stride * height;
                int keepEvery = (int)Math.Max(1, (count * frameBytes + MaxFrameMemory - 1) / MaxFrameMemory);

                var frames = new List<BitmapSource>();
                var delays = new List<TimeSpan>();

                // One drawing surface for every frame, and one bitmap to draw the previous result from
                var surface = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                var baseBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

                byte[] canvas = null;       // everything drawn so far
                byte[] beforeFrame = null;  // the canvas before the last frame, for "restore previous"
                Int32Rect lastArea = default;
                int lastDisposal = 0;

                for (int i = 0; i < count; i++)
                {
                    var frame = decoder.Frames[i];
                    var area = GetFrameArea(frame);
                    var delay = GetDelay(frame);
                    int disposal = GetDisposal(frame);

                    // What the previous frame's disposal leaves behind
                    byte[] basePixels = lastDisposal == 3 && beforeFrame != null ? beforeFrame : canvas;

                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        if (basePixels != null)
                        {
                            baseBitmap.WritePixels(new Int32Rect(0, 0, width, height), basePixels, stride, 0);
                            if (lastDisposal == 2)
                            {
                                // The previous frame's area goes back to the background: draw the canvas around it only
                                dc.PushClip(new CombinedGeometry(
                                    GeometryCombineMode.Exclude,
                                    new RectangleGeometry(everything),
                                    new RectangleGeometry(ToRect(lastArea, scale))));
                                dc.DrawImage(baseBitmap, everything);
                                dc.Pop();
                            }
                            else
                            {
                                dc.DrawImage(baseBitmap, everything);
                            }
                        }

                        dc.DrawImage(frame, ToRect(area, scale));
                    }

                    surface.Clear();
                    surface.Render(visual);
                    var composed = new byte[frameBytes];
                    surface.CopyPixels(composed, stride, 0);

                    beforeFrame = canvas;
                    canvas = composed;
                    lastArea = area;
                    lastDisposal = disposal;

                    // Fast or surplus frames fold into the frame before, but never the last one while only
                    // one frame is kept: a short blink GIF must stay an animation
                    bool keepForAnimation = i == count - 1 && frames.Count < 2;
                    bool absorb = frames.Count > 0 && !keepForAnimation
                        && (delays[^1] < MinFrameDelay || i % keepEvery != 0);
                    if (absorb)
                    {
                        delays[^1] += delay;
                        continue;
                    }

                    frames.Add(ToFrozenBitmap(composed, width, height));
                    delays.Add(delay);
                }

                if (frames.Count < 2)
                    return null;

                for (int i = 0; i < delays.Count; i++)
                {
                    if (delays[i] < MinFrameDelay)
                    {
                        delays[i] = MinFrameDelay;
                    }
                }

                var animation = new AnimatedBackground(width, height, frames, delays);
                quarterTurns = ((quarterTurns % 4) + 4) % 4;
                for (int turn = 0; turn < quarterTurns; turn++)
                {
                    animation = Rotate(animation, true);
                }

                return animation;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Turns every frame a quarter turn. Plain pixel copying, quick enough for the UI thread.
        /// </summary>
        public static AnimatedBackground Rotate(AnimatedBackground animation, bool clockwise)
        {
            int width = animation.PixelWidth;
            int height = animation.PixelHeight;
            var pixels = new byte[width * height * 4];
            var frames = new List<BitmapSource>(animation.Frames.Count);
            foreach (var frame in animation.Frames)
            {
                frame.CopyPixels(pixels, width * 4, 0);
                var source = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
                var rotated = new byte[pixels.Length];
                var target = MemoryMarshal.Cast<byte, uint>(rotated.AsSpan());

                // The turned picture is height wide and width tall
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int newX = clockwise ? height - 1 - y : y;
                        int newY = clockwise ? x : width - 1 - x;
                        target[newY * height + newX] = source[y * width + x];
                    }
                }

                frames.Add(ToFrozenBitmap(rotated, height, width));
            }

            return new AnimatedBackground(height, width, frames, animation.Delays);
        }

        /// <summary>
        /// A plain bitmap from Pbgra32 pixels; it keeps its own copy, so the array can be reused or dropped.
        /// </summary>
        private static BitmapSource ToFrozenBitmap(byte[] pixels, int width, int height)
        {
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static Rect ToRect(Int32Rect area, double scale) =>
            new Rect(area.X * scale, area.Y * scale, area.Width * scale, area.Height * scale);

        private static (int Width, int Height) GetScreenSize(GifBitmapDecoder decoder)
        {
            try
            {
                if (decoder.Metadata is BitmapMetadata metadata
                    && metadata.GetQuery("/logscrdesc/Width") is ushort width
                    && metadata.GetQuery("/logscrdesc/Height") is ushort height
                    && width > 0 && height > 0)
                {
                    return (width, height);
                }
            }
            catch { }

            var first = decoder.Frames[0];
            return (first.PixelWidth, first.PixelHeight);
        }

        private static Int32Rect GetFrameArea(BitmapFrame frame)
        {
            int left = 0, top = 0;
            try
            {
                if (frame.Metadata is BitmapMetadata metadata)
                {
                    if (metadata.GetQuery("/imgdesc/Left") is ushort l) left = l;
                    if (metadata.GetQuery("/imgdesc/Top") is ushort t) top = t;
                }
            }
            catch { }

            return new Int32Rect(left, top, frame.PixelWidth, frame.PixelHeight);
        }

        private static TimeSpan GetDelay(BitmapFrame frame)
        {
            try
            {
                if (frame.Metadata is BitmapMetadata metadata
                    && metadata.GetQuery("/grctlext/Delay") is ushort hundredths
                    && hundredths > 1)
                {
                    return TimeSpan.FromMilliseconds(hundredths * 10);
                }
            }
            catch { }

            return DefaultFrameDelay;
        }

        private static int GetDisposal(BitmapFrame frame)
        {
            try
            {
                if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("/grctlext/Disposal") is byte disposal)
                {
                    return disposal;
                }
            }
            catch { }

            return 0;
        }
    }
}
