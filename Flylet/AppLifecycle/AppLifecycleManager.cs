using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Flylet.AppLifecycle
{
    public class AppLifecycleManager
    {
        /// <summary>
        /// This constant allows us to distinguish between beta and stable releases and will helps us to enable/disable features for a certain build.<br/>
        /// This value could be changed by adding or removing '-beta' suffix to the app version in the *.csproj.<br/>
        /// <strong>For example:</strong> 'x.y.z.i-beta' would make this value <see langword="true" /> and removing '-beta' would make this <see langword="false"/>.
        /// </summary>
        public const bool IsBuildBetaChannel =
#if BETA
            true;
#else
            false;

#endif

        #region App activation & Single instancing

        private static readonly Mutex mutex = new(true, Program.AppName);

        /// <summary>
        /// Starts the application as single instance and redirects the command line arguments from subsequent instances to the first instance.
        /// </summary>
        /// <param name="args">Commandline arguments to process or pass to the first instance.</param>
        /// <param name="action">The action to perform after the first instance has been initialized.</param>
        public static void StartApplication(string[] args, Action action)
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                ProcessCommandLineArgs(args);

                CreateRemoteService(Program.AppName);

                action();
            }
            else
            {
                SignalFirstInstance(Program.AppName, args);
            }
        }

        // A second launch that breaks off mid-message, or a command that throws, must not take the running
        // copy down: an exception leaving this async void would end the process
        private static async void CreateRemoteService(string channelName)
        {
            while (true)
            {
                try
                {
                    using NamedPipeServerStream pipeServer = new(channelName, PipeDirection.In);
                    await pipeServer.WaitForConnectionAsync().ConfigureAwait(false);
                    using StreamReader reader = new(pipeServer);
                    var rawArgs = await reader.ReadToEndAsync().ConfigureAwait(false);

                    IList<string> args = rawArgs.Split(JumpListHelper.arg_delimiter);

                    ProcessCommandLineArgs(args, false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"{nameof(AppLifecycleManager)}: {ex.Message}");
                    // Don't spin if the pipe can't be created at all
                    await System.Threading.Tasks.Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        public static void ProcessCommandLineArgs(IList<string> args, bool isFirstInstance = true)
        {
            string arg = string.Empty;

            if (args?.Count > 0)
            {
                arg = args[0];
            }

            if (arg == string.Empty)
            {
                // A second launch always opens Settings. The first one does too when the user opened
                // it; a sign-in launch from the startup task stays quiet in the tray
                if (!isFirstInstance || Helpers.StartupHelper.IsUserLaunch())
                {
                    Program.RunCommand(RunCommandType.ShowSettings);
                }
            }
            else if (arg.ToLowerInvariant() == JumpListHelper.arg_settings)
            {
                Program.RunCommand(RunCommandType.ShowSettings);
            }
            else if (arg.ToLowerInvariant() == JumpListHelper.arg_restore)
            {
                Program.RunCommand(RunCommandType.RestoreDefault);
            }
            else if (arg.ToLowerInvariant() == JumpListHelper.arg_exit)
            {
                Program.RunCommand(RunCommandType.SafeExit);
            }
            else if (arg.ToLowerInvariant() == JumpListHelper.arg_appupdated)
            {
                Program.RunCommand(RunCommandType.AppUpdated);
            }
        }

        private static void SignalFirstInstance(string channelName, string[] args)
        {
            string rawArgs = string.Empty;
            foreach (var arg in args)
            {
                rawArgs += arg + JumpListHelper.arg_delimiter;
            }

            // The first copy may still be starting, or busy with another launch: give it a moment. If it
            // never answers, this copy still just exits instead of crashing
            try
            {
                using NamedPipeClientStream pipeClient = new(".", channelName, PipeDirection.Out);
                pipeClient.Connect(2000);

                using StreamWriter writer = new(pipeClient) { AutoFlush = true };
                writer.Write(rawArgs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(AppLifecycleManager)}: {ex.Message}");
            }

            Environment.Exit(0);
        }

        #endregion

        /// <summary>
        /// 😠 You did your best application. Now, <strong>prepare to die!</strong> <i>*pew*</i> <i>*pew*</i> <i>*pew*</i>.
        /// </summary>
        /// <remarks>
        /// There was an issue where the <see cref="mutex"/> still being held by this instance which prevented the app from restarting after an update.
        /// So, after registering for restart, we also have to release the <see cref="mutex"/>.
        /// <para>
        /// Runs on every WM_QUERYENDSESSION, and a shutdown can be cancelled (an app asking to save) and then
        /// asked for again, so a second call must do nothing instead of throwing on the disposed mutex.
        /// </para>
        /// </remarks>
        public static void PrepareToDie()
        {
            if (Interlocked.Exchange(ref hasPreparedToDie, 1) == 1)
                return;

            try
            {
                mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(AppLifecycleManager)}: {ex.Message}");
            }

            mutex.Dispose();
        }

        private static int hasPreparedToDie;
    }
}
