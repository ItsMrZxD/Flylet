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
        }
    }
}
