using Flylet.Core.Threading;
using Flylet.Core.Utilities;
using NPSMLib;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Flylet.Core.Media.Control
{
    public class NowPlayingMediaSessionManager : MediaSessionManager
    {
        private NowPlayingSessionManager NPSessionManager;

        public override async void OnEnabled()
        {
            NpsmService.Started += NpsmService_Started;
            await OnNpsmServiceStartedSafe();
        }

        private async void NpsmService_Started(object sender, EventArgs e)
        {
            await OnNpsmServiceStartedSafe();
        }

        // OnNpsmServiceStarted's own catch cleans up again, and that cleanup can throw too
        private async Task OnNpsmServiceStartedSafe()
        {
            try
            {
                await OnNpsmServiceStarted();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSessionManager)}: {ex.Message}");
            }
        }

        private async Task OnNpsmServiceStarted()
        {
            //Example: explorer.exe crashes, the NPSMLib still holds the "link"
            //THEN explorer.exe restarts and NPSM restarts too, reloading all NPSM sessions

            //Now recreate NPSessionManager.

            try
            {
                await CleanupNPSM();

                NPSessionManager = new();
                NPSessionManager.SessionListChanged += NPSessionsChanged;

                await LoadSessions();
            }
            catch
            {
                //This is in case NPSM dies immediately after sending a wnf notification

                await CleanupNPSM();
            }
        }

        // NPSM is a COM service that can go away mid-call (explorer restarting, for example); an exception
        // leaving this async void would end the process
        private async void NPSessionsChanged(object sender, NowPlayingSessionManagerEventArgs e)
        {
            try
            {
                await NPSessionsChangedUI(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSessionManager)}: {ex.Message}");
            }
        }

        private async UiTask NPSessionsChangedUI(NowPlayingSessionManagerEventArgs e)
        {
            OnSessionsChanged(e.NowPlayingSessionInfo, e.NotificationType);
        }

        private void OnSessionsChanged(NowPlayingSessionInfo nowPlayingSessionInfo, NowPlayingSessionManagerNotificationType notificationType)
        {
            nowPlayingSessionInfo.GetInfo(out _, out uint pid, out _);

            if (NPSessionManager == null)
            {
                //T H I S    S H O U L D    N E V E R    H A P P E N.
                return;
            }

            // Any change: match our sessions to NPSM's list. A reopened app (Firefox) is a new session, and a
            // missed or failed created/disconnected notification would otherwise leave a dead session behind
            // or the new one out for good
            bool changed = SyncSessions();

            if (notificationType == NowPlayingSessionManagerNotificationType.SessionCreated)
            {
                if (FindMediaSession(nowPlayingSessionInfo) == null)
                {
                    var session = NPSessionManager.FindSession(nowPlayingSessionInfo);

                    if (session != null && TryCreateMediaSession(session, out var mediaSession))
                    {
                        MediaSessions.Add(mediaSession);
                        changed = true;
                    }
                }
            }
            else if (notificationType == NowPlayingSessionManagerNotificationType.CurrentSessionChanged)
            {
                var mediaSession = FindMediaSession(nowPlayingSessionInfo);

                if (mediaSession != null)
                {
                    CurrentMediaSession = mediaSession;
                    foreach (var s in MediaSessions)
                    {
                        s.IsCurrent = false;
                    }

                    mediaSession.IsCurrent = true;
                    RaiseCurrentMediaSessionChanged();
                }
            }
            else if (notificationType == NowPlayingSessionManagerNotificationType.SessionDisconnected)
            {
                var mediaSession = FindMediaSession(nowPlayingSessionInfo);

                if (mediaSession != null)
                {
                    mediaSession.Disconnect();
                    MediaSessions.Remove(mediaSession);
                    changed = true;
                }
            }

            if (changed)
            {
                RaiseMediaSessionsChanged();
            }
        }

        /// <summary>
        /// Adds NPSM's sessions we don't have yet and drops the ones NPSM no longer lists.
        /// </summary>
        /// <returns>Whether <see cref="MediaSessionManager.MediaSessions"/> changed.</returns>
        private bool SyncSessions()
        {
            NowPlayingSession[] sessions;
            try
            {
                sessions = NPSessionManager.GetSessions();
            }
            catch (Exception ex)
            {
                // Without the list, leave the sessions as they are
                System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSessionManager)}: no session list: {ex.Message}");
                return false;
            }

            var current = sessions
                .Select(s => (Session: s, Info: TryGetSessionInfo(s)))
                .Where(s => s.Info != null)
                .ToList();

            bool changed = false;

            foreach (var mediaSession in MediaSessions.Cast<NowPlayingMediaSession>().ToList())
            {
                if (!current.Any(s => SameSession(s.Info, mediaSession.SessionInfo)))
                {
                    mediaSession.Disconnect();
                    MediaSessions.Remove(mediaSession);
                    changed = true;
                }
            }

            foreach (var (session, info) in current)
            {
                if (FindMediaSession(info) == null && TryCreateMediaSession(session, out var mediaSession))
                {
                    MediaSessions.Add(mediaSession);
                    changed = true;
                }
            }

            return changed;
        }

        private NowPlayingMediaSession FindMediaSession(NowPlayingSessionInfo info) => MediaSessions
            .Cast<NowPlayingMediaSession>()
            .FirstOrDefault(x => SameSession(x.SessionInfo, info));

        // NPSM compares the two itself, and the call fails if it has gone away
        private static bool SameSession(NowPlayingSessionInfo a, NowPlayingSessionInfo b)
        {
            if (a == null || b == null)
                return false;

            try
            {
                return a.Equals(b);
            }
            catch
            {
                return false;
            }
        }

        private static NowPlayingSessionInfo TryGetSessionInfo(NowPlayingSession session)
        {
            try
            {
                return session.GetSessionInfo();
            }
            catch
            {
                return null;
            }
        }

        private async UiTask ClearSessions()
        {
            foreach (var session in MediaSessions)
            {
                session.Disconnect();
            }

            MediaSessions.Clear();

            RaiseMediaSessionsChanged();
        }

        private async UiTask LoadSessions()
        {
            await ClearSessions();

            if (NPSessionManager != null)
            {
                var sessions = NPSessionManager.GetSessions();

                foreach (var session in sessions)
                {
                    if (TryCreateMediaSession(session, out var mediaSession))
                    {
                        MediaSessions.Add(mediaSession);
                    }
                }
            }

            RaiseMediaSessionsChanged();
        }

        // One app's session failing to load must not crash Flylet or drop every other session (GitHub issue #1)
        private static bool TryCreateMediaSession(NowPlayingSession session, out NowPlayingMediaSession mediaSession)
        {
            try
            {
                mediaSession = new NowPlayingMediaSession(session);
                return true;
            }
            catch
            {
                mediaSession = null;
                return false;
            }
        }

        public override async void OnDisabled()
        {
            NpsmService.Started -= NpsmService_Started;
            try
            {
                await CleanupNPSM();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSessionManager)}: {ex.Message}");
            }
        }

        private async Task CleanupNPSM()
        {
            if (NPSessionManager != null)
            {
                NPSessionManager.SessionListChanged -= NPSessionsChanged;
                NPSessionManager = null;
            }

            await ClearSessions();
        }
    }
}
