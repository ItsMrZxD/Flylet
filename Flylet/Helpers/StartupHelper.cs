using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Flylet.Helpers
{
    internal class StartupHelper
    {
        private const string StartupId = "FlyletStartupId";

        /// <summary>
        /// Whether the user opened this process (Start, search, the Store's Open button), as opposed to
        /// Windows starting it at sign-in through the startup task.
        /// </summary>
        /// <remarks>
        /// Only a confirmed Launch counts, so a startup-task launch, an unknown kind or a failure all
        /// stay quiet. Packaged apps only get the activation args on the first call, so call it once.
        /// </remarks>
        public static bool IsUserLaunch()
        {
            try
            {
                return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.Launch;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> GetRunAtStartupEnabled()
        {
            try
            {
                StartupTask startupTask = await StartupTask.GetAsync(StartupId);

                return startupTask.State == StartupTaskState.Enabled;
            }
            catch { return true; }
        }

        public static async void SetRunAtStartupEnabled(bool value)
        {
            try
            {
                StartupTask startupTask = await StartupTask.GetAsync(StartupId);

                if (value)
                {
                    await startupTask.RequestEnableAsync();
                }
                else
                {
                    startupTask.Disable();
                }
            }
            catch { }
        }
    }
}
