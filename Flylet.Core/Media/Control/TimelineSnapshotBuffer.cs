using System;
using System.Collections.Generic;

namespace Flylet.Core.Media.Control
{
    /// <summary>
    /// A timeline a source reported, and when Flylet received it.
    /// </summary>
    public readonly record struct ReceivedTimeline(DateTimeOffset ReceivedTime, TimelineSnapshot Snapshot);

    /// <summary>
    /// Keeps the timelines a session reported in the last few seconds.
    /// </summary>
    /// <remarks>
    /// Firefox reports a new song's length for only about 60 ms, and sometimes just before the new title.
    /// The title change forgets the old song's timeline, so the timelines received shortly before it are
    /// replayed from here. Thread-safe: reports arrive on NPSM's threads and are replayed on the UI thread.
    /// </remarks>
    public sealed class TimelineSnapshotBuffer
    {
        public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(5);

        private readonly List<ReceivedTimeline> entries = new();
        private readonly object gate = new();

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return entries.Count;
                }
            }
        }

        public void Add(DateTimeOffset receivedTime, TimelineSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            lock (gate)
            {
                entries.Add(new ReceivedTimeline(receivedTime, snapshot));
                RemoveOlderThan(receivedTime - MaxAge);
            }
        }

        /// <summary>
        /// The timelines received within <paramref name="window"/> before <paramref name="now"/>, oldest first.
        /// </summary>
        public IReadOnlyList<ReceivedTimeline> GetRecent(DateTimeOffset now, TimeSpan window)
        {
            lock (gate)
            {
                RemoveOlderThan(now - MaxAge);

                var recent = new List<ReceivedTimeline>();
                foreach (var entry in entries)
                {
                    if (entry.ReceivedTime >= now - window && entry.ReceivedTime <= now)
                    {
                        recent.Add(entry);
                    }
                }

                recent.Sort((a, b) => a.ReceivedTime.CompareTo(b.ReceivedTime));
                return recent;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                entries.Clear();
            }
        }

        private void RemoveOlderThan(DateTimeOffset cutoff) => entries.RemoveAll(e => e.ReceivedTime < cutoff);
    }
}
