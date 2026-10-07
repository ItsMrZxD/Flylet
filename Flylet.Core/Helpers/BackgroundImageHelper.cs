using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// Imports the photo used as the flyout background: a small, upright copy kept in the app's own
    /// storage, so the original can be moved or deleted.
    /// </summary>
    public static class BackgroundImageHelper
    {
        /// <summary>
        /// Longest side of the stored copy. Flyouts are 360 px wide and the photo is blurred, so
        /// this is plenty even at 200% scaling.
        /// </summary>
        public const int MaxSize = 960;

        /// <summary>
        /// Decodes any picture WIC can read, turns it upright, shrinks it to <see cref="MaxSize"/>
        /// and saves it as a JPEG at <paramref name="destinationPath"/>.
        /// </summary>
        /// <returns>False if the file isn't a picture that can be decoded; the destination is left untouched.</returns>
        public static bool TryImport(string sourcePath, string destinationPath)
        {
            string tempPath = destinationPath + ".tmp";
            try
            {
                BitmapSource image;
                using (var stream = File.OpenRead(sourcePath))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                    image = decoder.Frames[0];
                }

                image = ApplyExifOrientation(image, GetExifOrientation(image));

                double scale = Math.Min(1.0, (double)MaxSize / Math.Max(image.PixelWidth, image.PixelHeight));
                if (scale < 1.0)
                {
                    image = new TransformedBitmap(image, new ScaleTransform(scale, scale));
                }

                // JPEG has no alpha; converting first keeps transparent PNGs from turning black at random
                image = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);

                var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var output = File.Create(tempPath))
                {
                    encoder.Save(output);
                }

                File.Move(tempPath, destinationPath, true);
                return true;
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Loads the stored copy without keeping the file open, or null if there is none.
        /// </summary>
        public static ImageSource TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Phone photos are often stored sideways with an EXIF tag saying how to turn them.
        /// </summary>
        private static int GetExifOrientation(BitmapSource image)
        {
            try
            {
                if (image.Metadata is BitmapMetadata metadata
                    && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort orientation)
                {
                    return orientation;
                }
            }
            catch { }

            return 1;
        }

        public static BitmapSource ApplyExifOrientation(BitmapSource image, int orientation)
        {
            double angle = orientation switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0,
            };

            return angle == 0 ? image : new TransformedBitmap(image, new RotateTransform(angle));
        }
    }
}
