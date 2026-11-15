using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Flylet.Core.Audio
{
    /// <summary>
    /// One row of the volume mixer: an app and all of its audio streams on the current device, which move together.
    /// Lives on the UI thread.
    /// </summary>
    public sealed class AppAudioEntry : ObservableObject
    {
        internal readonly List<AudioSessionControl> Sessions = new();
        private bool syncing;

        internal AppAudioEntry(string key, uint processId, string exePath, bool isSystemSounds, string name)
        {
            Key = key;
            ProcessId = processId;
            ExePath = exePath;
            IsSystemSounds = isSystemSounds;
            this.name = name;
        }

        public string Key { get; }
        public uint ProcessId { get; }
        public string ExePath { get; }
        public bool IsSystemSounds { get; }

        private string name;

        public string Name
        {
            get => name;
            internal set
            {
                if (SetProperty(ref name, value))
                    OnPropertyChanged(nameof(Initial));
            }
        }

        public string Initial => AppAudioNaming.InitialOf(name);

        private ImageSource icon;

        /// <summary>
        /// The app's icon, or null when there's none (the row then shows <see cref="Initial"/> and the name).
        /// </summary>
        public ImageSource Icon
        {
            get => icon;
            internal set
            {
                if (SetProperty(ref icon, value))
                {
                    OnPropertyChanged(nameof(HasIcon));
                    OnPropertyChanged(nameof(ShowNameLabel));
                }
            }
        }

        public bool HasIcon => icon != null;

        /// <summary>
        /// Rows without an icon show their name next to the slider, so every row can be told apart.
        /// </summary>
        public bool ShowNameLabel => icon == null;

        private double volume;

        /// <summary>
        /// 0-100. Setting it changes every stream of the app.
        /// </summary>
        public double Volume
        {
            get => volume;
            set
            {
                value = Math.Clamp(value, 0, 100);
                if (!SetProperty(ref volume, value))
                    return;
                OnPropertyChanged(nameof(VolumeText));
                if (syncing)
                    return;

                foreach (var s in Sessions)
                    Try(() => s.SimpleAudioVolume.Volume = (float)(value / 100));
            }
        }

        public string VolumeText => Math.Round(volume).ToString("00");

        private bool muted;

        public bool Muted
        {
            get => muted;
            set
            {
                if (!SetProperty(ref muted, value) || syncing)
                    return;

                foreach (var s in Sessions)
                    Try(() => s.SimpleAudioVolume.Mute = value);
            }
        }

        /// <summary>
        /// Reads the streams' current volume and mute (changed by the app or Windows' mixer) without writing back.
        /// </summary>
        internal void SyncFromSessions()
        {
            if (Sessions.Count == 0)
                return;

            syncing = true;
            try
            {
                float max = 0;
                bool allMuted = true;
                foreach (var s in Sessions)
                {
                    try
                    {
                        max = Math.Max(max, s.SimpleAudioVolume.Volume);
                        allMuted &= s.SimpleAudioVolume.Mute;
                    }
                    catch { }
                }
                Volume = max * 100;
                Muted = allMuted;
            }
            finally { syncing = false; }
        }

        private static void Try(Action action)
        {
            // A stream can end between reading the list and touching it
            try { action(); } catch { }
        }
    }
}
