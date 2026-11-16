using CommunityToolkit.Mvvm.ComponentModel;
using Flylet.Core.Interop;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// The apps playing sound on the default output device, one <see cref="AppAudioEntry"/> per app, kept up to date
    /// from Windows' session events (no polling). Flylet's own stream is left out. Use from the UI thread.
    /// </summary>
    public sealed class AppAudioMixer : ObservableObject, IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly string systemSoundsName;
        private readonly uint ownProcessId = (uint)Environment.ProcessId;

        private MMDevice device;
        private AudioSessionManager manager;
        private bool resyncQueued;

        // Every stream we listen to, by Windows' instance id, so events are registered once per stream
        private readonly Dictionary<string, (AudioSessionControl Control, SessionEvents Events)> watched = new();

        // Windows doesn't always end a closed app's streams at once, so the processes are watched too
        private readonly Dictionary<uint, IDisposable> exitWatches = new();

        public AppAudioMixer(Dispatcher dispatcher, string systemSoundsName)
        {
            this.dispatcher = dispatcher;
            this.systemSoundsName = systemSoundsName;
        }

        public ObservableCollection<AppAudioEntry> Apps { get; } = new();

        private bool hasApps;

        public bool HasApps
        {
            get => hasApps;
            private set => SetProperty(ref hasApps, value);
        }

        /// <summary>
        /// Follows a new default device (null = none). Takes the device's id so the COM objects are made here.
        /// </summary>
        public void SetDevice(string deviceId)
        {
            var oldManager = manager;
            StopWatching();
            DisposeDevice(oldManager);

            if (!string.IsNullOrEmpty(deviceId))
            {
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    device = enumerator.GetDevice(deviceId);
                    manager = device.AudioSessionManager;
                    manager.OnSessionCreated += Manager_OnSessionCreated;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Volume mixer: opening the device failed: {ex.Message}");
                    manager = null;
                }
            }

            Resync();
        }

        private void Manager_OnSessionCreated(object sender, IAudioSessionControl newSession) => QueueResync();

        /// <summary>
        /// Re-reads the streams soon, once, however many events arrive together.
        /// </summary>
        private void QueueResync()
        {
            if (resyncQueued)
                return;
            resyncQueued = true;
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                resyncQueued = false;
                Resync();
            });
        }

        /// <summary>
        /// Reads the device's streams and updates the rows: new apps added, gone apps removed, the rest kept.
        /// </summary>
        public void Resync()
        {
            var groups = new Dictionary<string, List<AudioSessionControl>>();
            var firstOf = new Dictionary<string, AudioSessionControl>();
            var seenIds = new HashSet<string>();
            var livePids = new HashSet<uint>();

            if (manager != null)
            {
                try
                {
                    manager.RefreshSessions();
                    var sessions = manager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var fresh = sessions[i];
                        if (fresh.State == AudioSessionState.AudioSessionStateExpired)
                            continue;

                        uint pid = fresh.GetProcessID;
                        bool system = fresh.IsSystemSoundsSession;
                        if (!system && (pid == ownProcessId || !AppAudioIdentity.IsProcessRunning(pid)))
                            continue;
                        if (!system && pid != 0)
                            livePids.Add(pid);

                        string id = fresh.GetSessionInstanceIdentifier;
                        seenIds.Add(id);
                        if (!watched.TryGetValue(id, out var w))
                        {
                            var events = new SessionEvents(this, id);
                            fresh.RegisterEventClient(events);
                            w = (fresh, events);
                            watched[id] = w;
                        }

                        string path = system ? null : NativeMethods.GetProcessImagePath(pid);
                        string key = AppAudioNaming.GroupKey(system, path, pid);
                        if (!groups.TryGetValue(key, out var list))
                        {
                            groups[key] = list = new List<AudioSessionControl>();
                            firstOf[key] = w.Control;
                        }
                        list.Add(w.Control);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Volume mixer: reading streams failed: {ex.Message}");
                }
            }

            foreach (var id in watched.Keys.Where(k => !seenIds.Contains(k)).ToList())
                Unwatch(id);
            UpdateExitWatches(livePids);

            // Drop rows whose app has no streams left
            for (int i = Apps.Count - 1; i >= 0; i--)
            {
                if (!groups.ContainsKey(Apps[i].Key))
                    Apps.RemoveAt(i);
            }

            foreach (var (key, controls) in groups)
            {
                var entry = Apps.FirstOrDefault(a => a.Key == key);
                bool isNew = entry == null;
                if (isNew)
                    entry = AddEntry(key, firstOf[key]);

                // A muted app stays muted when it starts another stream (an ad, a new tab)
                var added = controls.Where(c => !entry.Sessions.Contains(c)).ToList();
                if (!isNew && entry.Muted)
                {
                    foreach (var c in added)
                    {
                        try { c.SimpleAudioVolume.Mute = true; } catch { }
                    }
                }

                entry.Sessions.Clear();
                entry.Sessions.AddRange(controls);
                entry.SyncFromSessions();
            }

            SortRows();
            HasApps = Apps.Count > 0;
        }

        private AppAudioEntry AddEntry(string key, AudioSessionControl first)
        {
            uint pid = 0;
            bool system = false;
            string display = null, iconPath = null;
            try
            {
                pid = first.GetProcessID;
                system = first.IsSystemSoundsSession;
                display = AppAudioIdentity.LoadIndirectString(first.DisplayName);
                iconPath = first.IconPath;
            }
            catch { }

            string path = system ? null : NativeMethods.GetProcessImagePath(pid);
            string name = system ? systemSoundsName : NameFor(display, path, pid);

            var entry = new AppAudioEntry(key, pid, path, system, name);
            Apps.Add(entry);
            bool keepName = system || (!string.IsNullOrWhiteSpace(display) && !display.TrimStart().StartsWith("@"));
            AppAudioIcons.Load(entry, iconPath, keepName, dispatcher);
            return entry;
        }

        private static string NameFor(string display, string path, uint pid)
        {
            string description = null, processName = null;
            if (!string.IsNullOrEmpty(path))
            {
                try { description = FileVersionInfo.GetVersionInfo(path).FileDescription; } catch { }
            }
            else
            {
                try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { }
            }
            return AppAudioNaming.PickName(display, description, path, processName, pid);
        }

        private void SortRows()
        {
            var sorted = Apps.ToList();
            sorted.Sort((a, b) => AppAudioNaming.Compare(a.IsSystemSounds, a.Name, b.IsSystemSounds, b.Name));
            for (int i = 0; i < sorted.Count; i++)
            {
                int at = Apps.IndexOf(sorted[i]);
                if (at != i)
                    Apps.Move(at, i);
            }
        }

        private void Unwatch(string id)
        {
            if (!watched.Remove(id, out var w))
                return;
            // On failure NAudio keeps the callback and its finalizer tries again on the finalizer thread,
            // where an exception ends the app
            try { w.Control.UnRegisterEventClient(w.Events); } catch { GC.SuppressFinalize(w.Control); }
        }

        private void UpdateExitWatches(HashSet<uint> livePids)
        {
            foreach (var pid in exitWatches.Keys.Where(p => !livePids.Contains(p)).ToList())
            {
                exitWatches[pid]?.Dispose();
                exitWatches.Remove(pid);
            }
            foreach (var pid in livePids)
            {
                if (!exitWatches.ContainsKey(pid))
                    exitWatches[pid] = AppAudioIdentity.WatchProcessExit(pid, () => dispatcher.BeginInvoke(QueueResync));
            }
        }

        private void StopWatching()
        {
            UpdateExitWatches(new HashSet<uint>());

            foreach (var id in watched.Keys.ToList())
                Unwatch(id);

            if (manager != null)
            {
                manager.OnSessionCreated -= Manager_OnSessionCreated;
                manager = null;
            }
        }

        private void OnSessionVolumeChanged(string id)
        {
            if (!watched.TryGetValue(id, out var w))
                return;
            Apps.FirstOrDefault(a => a.Sessions.Contains(w.Control))?.SyncFromSessions();
        }

        // NAudio throws when Windows refuses to unregister (device gone, audio service restarted), and its
        // finalizers would try again on the finalizer thread, where an exception ends the app
        private void DisposeDevice(AudioSessionManager oldManager)
        {
            try { device?.Dispose(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Volume mixer: closing the device failed: {ex.Message}");
                GC.SuppressFinalize(device);
                if (oldManager != null)
                    GC.SuppressFinalize(oldManager);
            }
            device = null;
        }

        public void Dispose()
        {
            var oldManager = manager;
            StopWatching();
            DisposeDevice(oldManager);
            Apps.Clear();
            HasApps = false;
        }

        /// <summary>
        /// One stream's events. Windows calls these on its own threads, so they hop to the UI thread.
        /// </summary>
        private sealed class SessionEvents : IAudioSessionEventsHandler
        {
            private readonly AppAudioMixer owner;
            private readonly string id;

            public SessionEvents(AppAudioMixer owner, string id)
            {
                this.owner = owner;
                this.id = id;
            }

            public void OnVolumeChanged(float volume, bool isMuted)
                => owner.dispatcher.BeginInvoke(() => owner.OnSessionVolumeChanged(id));

            public void OnStateChanged(AudioSessionState state)
            {
                if (state == AudioSessionState.AudioSessionStateExpired)
                    owner.dispatcher.BeginInvoke(owner.QueueResync);
            }

            public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
                => owner.dispatcher.BeginInvoke(owner.QueueResync);

            public void OnDisplayNameChanged(string displayName) { }
            public void OnIconPathChanged(string iconPath) { }
            public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
            public void OnGroupingParamChanged(ref Guid groupingId) { }
        }
    }
}
