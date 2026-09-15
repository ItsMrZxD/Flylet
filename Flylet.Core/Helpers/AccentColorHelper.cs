using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// Picks an accent color from a media thumbnail, used for the media flyout's play button and timeline.
    /// </summary>
    public static class AccentColorHelper
    {
        private const int SampleSize = 32;
        private const int HueBuckets = 12;

        // Accents stay soft and fairly light so a dark icon on top of them stays readable
        public const double MinLightness = 0.62;
        public const double MaxLightness = 0.78;
        public const double MinSaturation = 0.45;
        public const double MaxSaturation = 0.85;

        public static bool TryGetAccentColor(ImageSource image, out Color color)
        {
            color = default;
            if (image is not BitmapSource bitmap || bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            {
                return false;
            }

            try
            {
                BitmapSource source = bitmap;
                double scale = (double)SampleSize / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
                if (scale < 1.0)
                {
                    source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                }
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

                int width = source.PixelWidth;
                int height = source.PixelHeight;
                var pixels = new byte[width * height * 4];
                source.CopyPixels(pixels, width * 4, 0);

                if (PickAccent(pixels) is not { } accent)
                {
                    return false;
                }

                color = Color.FromRgb(accent.R, accent.G, accent.B);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the most common vivid hue in BGRA32 pixels and softens it into an accent color.
        /// Returns null when the image has too little color, e.g. black-and-white artwork.
        /// </summary>
        public static (byte R, byte G, byte B)? PickAccent(ReadOnlySpan<byte> bgraPixels)
        {
            var weights = new double[HueBuckets];
            var reds = new double[HueBuckets];
            var greens = new double[HueBuckets];
            var blues = new double[HueBuckets];
            double totalWeight = 0;
            int opaquePixels = 0;

            for (int i = 0; i + 3 < bgraPixels.Length; i += 4)
            {
                if (bgraPixels[i + 3] < 128)
                {
                    continue;
                }
                opaquePixels++;

                double b = bgraPixels[i] / 255.0;
                double g = bgraPixels[i + 1] / 255.0;
                double r = bgraPixels[i + 2] / 255.0;
                var (hue, saturation, lightness) = RgbToHsl(r, g, b);

                // Grayish, near-black and near-white pixels say little about the artwork's color
                if (saturation < 0.25 || lightness < 0.15 || lightness > 0.9)
                {
                    continue;
                }

                int bucket = (int)(hue / 360.0 * HueBuckets) % HueBuckets;
                weights[bucket] += saturation;
                reds[bucket] += r * saturation;
                greens[bucket] += g * saturation;
                blues[bucket] += b * saturation;
                totalWeight += saturation;
            }

            // Mostly colorless images (a small colored logo on gray, for example) get no accent
            if (opaquePixels == 0 || totalWeight < opaquePixels * 0.05)
            {
                return null;
            }

            int best = 0;
            for (int i = 1; i < HueBuckets; i++)
            {
                if (weights[i] > weights[best])
                {
                    best = i;
                }
            }

            var (h, s, l) = RgbToHsl(reds[best] / weights[best], greens[best] / weights[best], blues[best] / weights[best]);
            var (red, green, blue) = HslToRgb(h, Math.Clamp(s, MinSaturation, MaxSaturation), Math.Clamp(l, MinLightness, MaxLightness));
            return (ToByte(red), ToByte(green), ToByte(blue));
        }

        /// <summary>
        /// Returns the color made lighter (positive amount) or darker (negative amount).
        /// </summary>
        public static Color Shade(Color color, double amount)
        {
            var (h, s, l) = RgbToHsl(color.R / 255.0, color.G / 255.0, color.B / 255.0);
            var (r, g, b) = HslToRgb(h, s, Math.Clamp(l + amount, 0, 1));
            return Color.FromArgb(color.A, ToByte(r), ToByte(g), ToByte(b));
        }

        public static (double Hue, double Saturation, double Lightness) RgbToHsl(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double lightness = (max + min) / 2;
            double delta = max - min;
            if (delta == 0)
            {
                return (0, 0, lightness);
            }

            double saturation = Math.Min(1, delta / (1 - Math.Abs(2 * lightness - 1)));
            double hue;
            if (max == r)
            {
                hue = 60 * ((g - b) / delta % 6);
            }
            else if (max == g)
            {
                hue = 60 * ((b - r) / delta + 2);
            }
            else
            {
                hue = 60 * ((r - g) / delta + 4);
            }

            if (hue < 0)
            {
                hue += 360;
            }
            return (hue, saturation, lightness);
        }

        private static (double R, double G, double B) HslToRgb(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
            double m = lightness - c / 2;
            var (r, g, b) = hue switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x),
            };
            return (r + m, g + m, b + m);
        }

        private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
    }
}
