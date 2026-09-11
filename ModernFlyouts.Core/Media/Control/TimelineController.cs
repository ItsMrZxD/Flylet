using System;
using System.Windows;
using System.Windows.Threading;

namespace ModernFlyouts.Core.Media.Control
{
    /// <summary>
    /// A timeline as reported by a media source.
    /// </summary>
    public sealed record TimelineSnapshot
    {
        public TimeSpan StartTime { get; init; }

        public TimeSpan EndTime { get; init; }

        public TimeSpan Position { get; init; }

        /// <summary>
        /// When <see cref="Position"/> was measured. Some sources never set this.
        /// </summary>
        public DateTimeOffset LastUpdatedTime { get; init; }

        public double? PlaybackRate { get; init; }

        public bool IsPlaying { get; init; }
    }

    /// <summary>
    /// Estimates the live playback position from the last real timeline a source reported.
    /// </summary>
    /// <remarks>
    /// Sources rarely report the position while playing. Firefox, for example, resets the timeline to
    /// position 0 and length 0 shortly after a video starts or seeks, and only reports the real position
    /// again on pause. This keeps counting from the last real report and ticks while playing.
    /// </remarks>
    public sealed class TimelineController : IDisposable
    {
        private readonly TimeProvider timeProvider;
        private readonly DispatcherTimer timer;

        private TimelineSnapshot snapshot;
        private TimeSpan basePosition;
        private DateTimeOffset baseTime;

        public TimelineController(TimeProvider timeProvider = null, Dispatcher dispatcher = null)
        {
            this.timeProvider = timeProvider ?? TimeProvider.System;
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += Timer_Tick;
        }

        /// <summary>
        /// Raised on every tick while playing.
        /// </summary>
        public event EventHandler PositionChanged;

        public TimeSpan Position => HasTimeline ? Estimate(timeProvider.GetUtcNow()) : TimeSpan.Zero;

        /// <summary>
        /// Whether a real timeline is known for the current track.
        /// </summary>
        public bool HasTimeline => snapshot != null && !IsEmpty(snapshot);

        public TimeSpan StartTime => HasTimeline ? snapshot.StartTime : TimeSpan.Zero;

        public TimeSpan EndTime => HasTimeline ? snapshot.EndTime : TimeSpan.Zero;

        public bool IsTicking => timer.IsEnabled;

        public void Update(TimelineSnapshot newSnapshot)
        {
            var now = timeProvider.GetUtcNow();

            if (IsEmpty(newSnapshot) && HasTimeline)
            {
                // No new information: keep the last real timeline and only take the new play state
                basePosition = Estimate(now);
                baseTime = now;
                snapshot = snapshot with { IsPlaying = newSnapshot.IsPlaying, PlaybackRate = newSnapshot.PlaybackRate };
            }
            else
            {
                bool isFresh = snapshot == null
                    || newSnapshot.Position != snapshot.Position
                    || newSnapshot.LastUpdatedTime != snapshot.LastUpdatedTime;

                if (isFresh)
                {
                    basePosition = newSnapshot.Position;
                    baseTime = newSnapshot.LastUpdatedTime.Year > 2000 ? newSnapshot.LastUpdatedTime : now;
                }
                else
                {
                    // The same position read again, e.g. on play/pause: carry on from the current estimate
                    basePosition = Estimate(now);
                    baseTime = now;
                }

                snapshot = newSnapshot;
            }

            timer.IsEnabled = snapshot.IsPlaying && HasTimeline;
        }

        /// <summary>
        /// Forgets the current timeline, e.g. when the track changes.
        /// </summary>
        public void Clear()
        {
            snapshot = null;
            timer.Stop();
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
        }

        // Position 0 with no length is what sources send when they have no timeline
        private static bool IsEmpty(TimelineSnapshot timeline) =>
            timeline.Position == TimeSpan.Zero && timeline.EndTime <= timeline.StartTime;

        private TimeSpan Estimate(DateTimeOffset now)
        {
            var position = basePosition;
            if (snapshot.IsPlaying && now > baseTime)
            {
                double rate = snapshot.PlaybackRate is > 0 ? snapshot.PlaybackRate.Value : 1.0;
                position += TimeSpan.FromTicks((long)((now - baseTime).Ticks * rate));
            }

            if (position < snapshot.StartTime)
            {
                return snapshot.StartTime;
            }

            // An end time at or before the start means the length is unknown (live streams)
            if (snapshot.EndTime > snapshot.StartTime && position > snapshot.EndTime)
            {
                return snapshot.EndTime;
            }

            return position;
        }

        private void Timer_Tick(object sender, EventArgs e) => PositionChanged?.Invoke(this, EventArgs.Empty);
    }
}
