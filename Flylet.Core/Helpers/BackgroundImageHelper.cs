using System;
using System.IO;
using System.Windows;
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
        /// Turns the stored copy a quarter turn and saves it back.
        /// </summary>
        /// <returns>False if the copy couldn't be read or written; the file is left as it was.</returns>
        public static bool TryRotate(string path, bool clockwise)
        {
            string tempPath = path + ".tmp";
            try
            {
                BitmapSource image;
                using (var stream = File.OpenRead(path))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                    image = decoder.Frames[0];
                }

                image = new TransformedBitmap(image, new RotateTransform(clockwise ? 90 : 270));

                var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var output = File.Create(tempPath))
                {
                    encoder.Save(output);
                }

                File.Move(tempPath, path, true);
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
        /// The part of the photo to show, as a fraction of the photo (0-1 each way), for a tile of the
        /// given shape. Its shape always matches the tile, so nothing gets stretched.
        /// </summary>
        /// <param name="imageAspect">Photo width divided by height.</param>
        /// <param name="tileAspect">Tile width divided by height.</param>
        /// <param name="zoom">1 shows as much of the photo as fits; 2 shows half of that, and so on.</param>
        /// <param name="positionX">0 is the left edge, 0.5 the middle, 1 the right edge.</param>
        /// <param name="positionY">0 is the top, 0.5 the middle, 1 the bottom.</param>
        public static Rect GetViewbox(double imageAspect, double tileAspect, double zoom, double positionX, double positionY)
        {
            if (!(imageAspect > 0) || !(tileAspect > 0))
                return new Rect(0, 0, 1, 1);

            zoom = Math.Clamp(zoom, 1, 10);
            positionX = Math.Clamp(positionX, 0, 1);
            positionY = Math.Clamp(positionY, 0, 1);

            double width = 1;
            double height = 1;
            if (imageAspect > tileAspect)
            {
                width = tileAspect / imageAspect;
            }
            else
            {
                height = imageAspect / tileAspect;
            }

            width /= zoom;
            height /= zoom;
            return new Rect((1 - width) * positionX, (1 - height) * positionY, width, height);
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
