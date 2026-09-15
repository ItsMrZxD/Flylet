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
            get => Assembly.GetExecutingAssembly().GetName().Version.ToString();
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
