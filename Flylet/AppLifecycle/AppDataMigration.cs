using Flylet.Helpers;
using Windows.Storage;

namespace Flylet.AppLifecycle
{
    public class AppDataMigration
    {
        /// <summary>
        /// Migrates unused application data into their new alternatives if they exists.
        /// </summary>
        public static void Perform()
        {
            try
            {
                string topBarEnabled = "TopBarEnabled";
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(topBarEnabled))
                {
                    AppDataHelper.TopBarVisibility = AppDataHelper.GetValue(true, topBarEnabled) ? UI.TopBarVisibility.Visible : UI.TopBarVisibility.AutoHide;
                    ApplicationData.Current.LocalSettings.Values.Remove(topBarEnabled);
                }
            }
            catch { }

            try
            {
                // 0.11 folded the thumbnail toggles into the media card's album art option. The background
                // toggle was saved (default on) but never drawn anything, so it maps to nothing.
                var values = ApplicationData.Current.LocalSettings.Values;
                string alignThumbnailToRight = "AlignGSMTCThumbnailToRight";
                if (values.ContainsKey(alignThumbnailToRight))
                {
                    if (AppDataHelper.GetValue(false, alignThumbnailToRight))
                    {
                        AppDataHelper.MediaCardArt = Core.Media.MediaCardArt.Right;
                    }
                    values.Remove(alignThumbnailToRight);
                }
                values.Remove("UseGSMTCThumbnailAsBackground");
            }
            catch { }
        }
    }
}
