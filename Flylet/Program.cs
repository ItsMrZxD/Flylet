using Flylet.AppLifecycle;
using Flylet.Core.Interop;
using Flylet.Helpers;
using System;
using System.Reflection;
using System.Threading;

namespace Flylet
{
    public class Program
    {
        public const string AppName = "Flylet";
        public const string AppHostName = "Flylet";

        [STAThread]
        private static void Main(string[] args)
        {
            Thread thread = new(() => {
                AppLifecycleManager.StartApplication(args, () =>
                {
                    HadSettingsAtStartup = HasAnySettings();

                    InitializePrivateUseClasses();

                    AppDataMigration.Perform();

                    NativeFlyoutHandler.Instance = new NativeFlyoutHandler();
                    NativeFlyoutHandler.Instance.Initialize();

                    LocalizationHelper.Initialize();

                    var app = new App();
                    app.Run();
                });
            });

            //If you lauch directly from the host bridge it won't be STA.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>
        /// True if Flylet had saved settings before this run wrote any: an update, not a fresh install. Versions
        /// before 0.11 didn't save the first-run time, so that alone can't tell them apart.
        /// </summary>
        internal static bool HadSettingsAtStartup { get; private set; }

        private static bool HasAnySettings()
        {
            try
            {
                return Windows.Storage.ApplicationData.Current.LocalSettings.Values.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        internal static void RunCommand(RunCommandType runCommandType)
        {
            switch (runCommandType)
            {
                case RunCommandType.ShowSettings:
                    {
                        if (FlyoutHandler.HasInitialized)
                        {
                            FlyoutHandler.ShowSettingsWindow();
                        }
                        else
                        {
                            FlyoutHandler.Initialized += (_, __) => FlyoutHandler.ShowSettingsWindow();
                        }
                        break;
                    }
                case RunCommandType.RestoreDefault:
                    {
                        NativeFlyoutHandler.Instance.VerifyNativeFlyoutCreated();
                        FlyoutHandler.SafelyExitApplication();
                        break;
                    }
                case RunCommandType.SafeExit:
                    {
                        FlyoutHandler.SafelyExitApplication();
                        break;
                    }
                case RunCommandType.AppUpdated:
                    {
                        //if (AppLifecycleManager.IsBuildBetaChannel)
                        //{
                        //    MessageBox.Show("App update successfully!", AppName);
                        //}

                        break;
                    }
                default:
                    break;
            }
        }

        public static string AppVersion
        {
            get
            {
                // The package version is the one the Store shows; the assembly version
                // (Directory.Build.props) was never bumped per release, so it's only the fallback.
                try
                {
                    var version = Windows.ApplicationModel.Package.Current.Id.Version;
                    return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                }
                catch { }

                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        internal static void InitializePrivateUseClasses()
        {
#if Screenshots
            FlyoutHandler.Initialized += (_, __) => Private.ScreenshotHelper.Initialize();
#endif
        }
    }

    internal enum RunCommandType
    {
        ShowSettings = 0,
        RestoreDefault = 1,
        SafeExit = 2,
        AppUpdated = 3
    }
}
