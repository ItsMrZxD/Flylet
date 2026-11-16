using CommunityToolkit.Mvvm.Input;
using Flylet.Core.AppInformation;
using Flylet.Core.Helpers;
using Flylet.Core.Threading;
using NPSMLib;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flylet.Core.Media.Control
{
    public class NowPlayingMediaSession : MediaSession
    {
        private NowPlayingSession NPSession;
        private SourceAppInfo sourceAppInfo;
        private MediaPlaybackDataSource mediaPlaybackDataSource;
        private int currentTrackNumber;
        private string sourceAppId = string.Empty;

        // Firefox reports a new song's length for only ~60 ms, sometimes just before the new title
        private static readonly TimeSpan TimelineReplayWindow = TimeSpan.FromSeconds(3);
        private readonly TimelineSnapshotBuffer recentTimelines = new();

        public NowPlayingSession NowPlayingSession => NPSession;

        /// <summary>
        /// Read once at creation, so finding this session later never calls into a session that has gone away.
        /// </summary>
        public NowPlayingSessionInfo SessionInfo { get; private set; }

        public NowPlayingMediaSession(NowPlayingSession nowPlayingSession)
        {
            NPSession = nowPlayingSession;
            Initialize();
        }

        private void Initialize()
        {
            if (NPSession == null)
                throw new NullReferenceException(nameof(NPSession));

            try
            {
                sourceAppId = NPSession.SourceAppId;
            }
            catch { }

            try
            {
                SessionInfo = NPSession.GetSessionInfo();
            }
            catch { }

            sourceAppInfo = SourceAppInfo.FromData(new SourceAppInfoData()
            {
                AppUserModelId = sourceAppId,
                ProcessId = NPSession.PID,
                MainWindowHandle = NPSession.Hwnd,
                DataType = SourceAppInfoDataType.FromProcessId
            });

            if (sourceAppInfo != null)
            {
                sourceAppInfo.InfoFetched += SourceAppInfo_InfoFetched;
                sourceAppInfo.FetchInfosAsync();
                ActivateMediaSourceCommand = new RelayCommand(sourceAppInfo.Activate, () => sourceAppInfo != null);
            }

            mediaPlaybackDataSource = NPSession.ActivateMediaPlaybackDataSource();

            if (mediaPlaybackDataSource == null)
                throw new InvalidOperationException(nameof(mediaPlaybackDataSource) + " should not be null");

            mediaPlaybackDataSource.MediaPlaybackDataChanged += MediaPlaybackDataSource_MediaPlaybackDataChanged;

            UpdateSessionInfo();
        }

        private void MediaPlaybackDataSource_MediaPlaybackDataChanged(object sender, MediaPlaybackDataChangedArgs e)
        {
            var dataChangedEvent = e.DataChangedEvent;

            // Read the timeline right here: Firefox's report of a new song's length is gone ~60 ms later,
            // often before the UI thread would get to it
            TimelineSnapshot timeline = null;
            if (dataChangedEvent is MediaPlaybackDataChangedEvent.PlaybackInfoChanged
                or MediaPlaybackDataChangedEvent.TimelinePropertiesChanged)
            {
                timeline = TryReadTimeline();
            }

            // BeginInvoke: holding NPSM's thread while the UI is busy would make us miss the next short report
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (mediaPlaybackDataSource == null)
                    return;

                // NPSM can go away mid-call. Invoke handed that exception back to NPSM's thread; from
                // BeginInvoke it would be unhandled on the UI thread and end the process
                try
                {
                    switch (dataChangedEvent)
                    {
                        case MediaPlaybackDataChangedEvent.MediaInfoChanged:
                            UpdateSessionInfo();
                            break;

                        case MediaPlaybackDataChangedEvent.PlaybackInfoChanged:
                            UpdatePlaybackInfo();
                            ApplyTimeline(timeline);
                            break;

                        case MediaPlaybackDataChangedEvent.TimelinePropertiesChanged:
                            ApplyTimeline(timeline);
                            break;

                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSession)}: {ex.Message}");
                }
            });
        }

        public override void Disconnect()
        {
            if (mediaPlaybackDataSource != null)
            {
                try
                {
                    mediaPlaybackDataSource.MediaPlaybackDataChanged -= MediaPlaybackDataSource_MediaPlaybackDataChanged;
                }
                catch { }
            }
            mediaPlaybackDataSource = null;
            NPSession = null;
            recentTimelines.Clear();
            StopTimeline();

            sourceAppInfo?.Dispose();
            sourceAppInfo = null;
        }

        private void SourceAppInfo_InfoFetched(object sender, EventArgs e)
        {
            if (sourceAppInfo != null)
            {
                sourceAppInfo.InfoFetched -= SourceAppInfo_InfoFetched;
                MediaSourceName = sourceAppInfo.DisplayName;

                if (BitmapHelper.TryCreateBitmapImageFromStream(sourceAppInfo.LogoStream, out var bitmap))
                {
                    MediaSourceIcon = bitmap;
                }
            }
        }

        #region Updating Properties

        private void UpdatePlaybackInfo()
        {
            if (mediaPlaybackDataSource != null)
            {
                var playback = mediaPlaybackDataSource.GetMediaPlaybackInfo();
                var valid = playback.PropsValid;
                IsPlaying = playback.PlaybackState == MediaPlaybackState.Playing;
                IsShuffleActive = valid.HasFlag(MediaPlaybackProps.ShuffleEnabled) ? playback.ShuffleEnabled : null;
                AutoRepeatMode = playback.RepeatMode switch
                {
                    MediaPlaybackRepeatMode.Unknown => MediaPlaybackAutoRepeatMode.None,
                    MediaPlaybackRepeatMode.None => MediaPlaybackAutoRepeatMode.None,
                    MediaPlaybackRepeatMode.Track => MediaPlaybackAutoRepeatMode.Track,
                    MediaPlaybackRepeatMode.List => MediaPlaybackAutoRepeatMode.List,
                    _ => throw new NotImplementedException(),
                };
            }
        }

        private void UpdateTimelineInfo()
        {
            if (mediaPlaybackDataSource != null)
            {
                var timeline = TryReadTimeline();
                if (timeline != null)
                {
                    UpdateTimeline(timeline);
                }
                else
                {
                    ClearTimeline();
                }
            }
        }

        /// <summary>
        /// Shows a timeline read on NPSM's thread, or reads it now if that read failed.
        /// </summary>
        private void ApplyTimeline(TimelineSnapshot timeline)
        {
            if (timeline != null)
            {
                UpdateTimeline(timeline);
            }
            else
            {
                UpdateTimelineInfo();
            }
        }

        /// <summary>
        /// Reads the source's timeline and remembers it for <see cref="ReplayRecentTimelines"/>. Any thread.
        /// </summary>
        private TimelineSnapshot TryReadTimeline()
        {
            var source = mediaPlaybackDataSource;
            if (source == null)
                return null;

            var receivedTime = DateTimeOffset.UtcNow;

            // Read the timeline even when the source can't seek: Edge on Windows 10 reports the song's
            // length and position without the PlaybackPosition capability (the card shows a progress bar then)
            try
            {
                var timeline = source.GetMediaTimelineProperties();
                var playback = source.GetMediaPlaybackInfo();
                var positionSetTime = timeline.PositionSetFileTime;

                var snapshot = new TimelineSnapshot
                {
                    StartTime = timeline.StartTime,
                    EndTime = timeline.EndTime,
                    Position = timeline.Position,
                    // Unset times come back as DateTime.MinValue, which can't be converted with a positive UTC offset
                    LastUpdatedTime = positionSetTime.Year > 2000 ? new DateTimeOffset(positionSetTime) : default,
                    PlaybackRate = playback.PlaybackRate,
                    IsPlaying = playback.PlaybackState == MediaPlaybackState.Playing
                };

                recentTimelines.Add(receivedTime, snapshot);
                return snapshot;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{nameof(NowPlayingMediaSession)}: no timeline: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// After a title change, shows the timelines that arrived shortly before it: Firefox sometimes
        /// reports the new song's length just before its title.
        /// </summary>
        private void ReplayRecentTimelines()
        {
            foreach (var (receivedTime, snapshot) in recentTimelines.GetRecent(DateTimeOffset.UtcNow, TimelineReplayWindow))
            {
                // Without the source's own time, count the position from when the report arrived
                UpdateTimeline(snapshot.LastUpdatedTime.Year > 2000 ? snapshot : snapshot with { LastUpdatedTime = receivedTime });
            }
        }

        private void UpdateSessionInfo()
        {
            if (mediaPlaybackDataSource == null)
                return;

            RaiseMediaPropertiesChanging();

            var mediaInfo = mediaPlaybackDataSource.GetMediaObjectInfo();

            // Setting a new title forgets the old song's timeline (see MediaSession)
            bool isNewTitle = !string.Equals(Title, mediaInfo.Title);
            Title = mediaInfo.Title;
            Artist = mediaInfo.Artist;
            if (currentTrackNumber != mediaInfo.TrackNumber)
            {
                TrackChangeDirection = (mediaInfo.TrackNumber - currentTrackNumber) switch
                {
                    > 0 => MediaPlaybackTrackChangeDirection.Forward,
                    < 0 => MediaPlaybackTrackChangeDirection.Backward,
                    _ => MediaPlaybackTrackChangeDirection.Unknown
                };
            }

            currentTrackNumber = (int)mediaInfo.TrackNumber;

            var playback = mediaPlaybackDataSource.GetMediaPlaybackInfo();

            var playbackCaps = playback.PlaybackCaps;
            IsPlayEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Play);
            IsPauseEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Pause);
            IsPlayOrPauseEnabled = IsPlayEnabled || IsPauseEnabled;
            IsPreviousEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Previous);
            IsNextEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Next);
            IsShuffleEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Shuffle);
            IsRepeatEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Repeat);
            IsStopEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.Stop);
            IsPlaybackPositionEnabled = playbackCaps.HasFlag(MediaPlaybackCapabilities.PlaybackPosition);

            PlaybackType = playback.PlaybackMode switch
            {
                MediaPlaybackMode.Unknown => MediaPlaybackType.Unknown,
                MediaPlaybackMode.Audio => MediaPlaybackType.Music,
                MediaPlaybackMode.Video => MediaPlaybackType.Video,
                MediaPlaybackMode.Image => MediaPlaybackType.Image,
                MediaPlaybackMode.Podcast => MediaPlaybackType.Music,
                MediaPlaybackMode.AudioBook => MediaPlaybackType.Music,
                _ => throw new NotImplementedException(),
            };

            if (isNewTitle)
            {
                ReplayRecentTimelines();
            }

            UpdateTimelineInfo();

            UpdatePlaybackInfo();

            Thumbnail = GetThumbnailImageSource();

            RaiseMediaPropertiesChanged();
        }

        #region Thumbnail fetching

        private const string SpotifyPackagedAUMID = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
        private const string SpotifyUnpackagedAUMID = "Spotify.exe";

        private bool IsSourceAppSpotify() => string.Equals(sourceAppId, SpotifyPackagedAUMID)
            || string.Equals(sourceAppId, SpotifyUnpackagedAUMID);

        private ImageSource GetThumbnailImageSource()
        {
            try
            {
                if (mediaPlaybackDataSource != null)
                {
                    var thumbnail = mediaPlaybackDataSource.GetThumbnailStream();
                    if (BitmapHelper.TryCreateBitmapImageFromStream(thumbnail, out var bitmap))
                    {
                        return GetModifiedThumbnail(bitmap);
                    }
                }
            }
            catch { }

            return null;
        }

        private BitmapSource GetModifiedThumbnail(BitmapSource bitmapSource)
        {
            if (IsSourceAppSpotify())
            {
                return GetModifiedThumbnailForSpotify(bitmapSource);
            }

            return bitmapSource;
        }

        /// <summary>
        /// Gets a modified version of the original thumbnail provided by Spotify.
        /// It crops and removes extra padding and Spotify branding from the original thumbnail provided by Spotify.
        /// </summary>
        /// <param name="bitmapSource">The original thumbnail provided by the source.</param>
        /// <returns>A cropped variant of the original thumbnail with extra padding and Spotify branding removed.</returns>
        /// <remarks>F*** you Spotify!</remarks>
        private BitmapSource GetModifiedThumbnailForSpotify(BitmapSource bitmapSource)
        {
            CroppedBitmap croppedBitmap = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                croppedBitmap = new CroppedBitmap(bitmapSource, new Int32Rect(33, 0, 234, 234));
            });

            return croppedBitmap;
        }

        #endregion

        #endregion

        #region Media Controlling

        protected override void ChangeAutoRepeatMode()
        {
            MediaPlaybackRepeatMode repeatMode = AutoRepeatMode switch
            {
                MediaPlaybackAutoRepeatMode.None => MediaPlaybackRepeatMode.List,
                MediaPlaybackAutoRepeatMode.List => MediaPlaybackRepeatMode.Track,
                MediaPlaybackAutoRepeatMode.Track => MediaPlaybackRepeatMode.None,
                _ => throw new NotImplementedException(),
            };

            mediaPlaybackDataSource?.SendRepeatModeChangeRequest(repeatMode);
        }

        protected override void ChangeShuffleActive()
        {
            if (IsShuffleActive.HasValue)
            {
                mediaPlaybackDataSource?.SendShuffleEnabledChangeRequest(!IsShuffleActive.Value);
            }
        }

        protected override void NextTrack()
        {
            mediaPlaybackDataSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Next);
        }

        protected override void Pause()
        {
            mediaPlaybackDataSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Pause);
        }

        protected override void Play()
        {
            mediaPlaybackDataSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Play);
        }

        private DebounceDispatcher debouncer = new();

        protected override void PlaybackPositionChanged(TimeSpan playbackPosition)
        {
            debouncer.Debounce(TimeSpan.FromMilliseconds(20), () =>
            {
                mediaPlaybackDataSource?.SendPlaybackPositionChangeRequest(playbackPosition);
            });
        }

        protected override void PreviousTrack()
        {
            mediaPlaybackDataSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Previous);
        }

        protected override void Stop()
        {
            mediaPlaybackDataSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Stop);
        }

        #endregion
    }
}
