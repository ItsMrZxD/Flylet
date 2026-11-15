using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Flylet.Core.Media.Control;
using Xunit;

namespace Flylet.Core.Tests
{
    public class TimelineSnapshotBufferTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan ReplayWindow = TimeSpan.FromSeconds(3);

        private static TimelineSnapshot Report(double positionSeconds, double endSeconds) => new()
        {
            Position = TimeSpan.FromSeconds(positionSeconds),
            EndTime = TimeSpan.FromSeconds(endSeconds),
            PlaybackRate = 1,
            IsPlaying = true
        };

        private static readonly TimelineSnapshot Empty = new() { PlaybackRate = 1, IsPlaying = true };

        [Fact]
        public void GetRecent_ReturnsOnlyTheWindow_OldestFirst()
        {
            var buffer = new TimelineSnapshotBuffer();
            buffer.Add(Start, Report(1, 100));
            buffer.Add(Start.AddSeconds(2), Report(2, 200));
            buffer.Add(Start.AddSeconds(4), Report(3, 300));

            var recent = buffer.GetRecent(Start.AddSeconds(4.5), ReplayWindow);

            Assert.Equal(new[] { 200.0, 300.0 }, recent.Select(r => r.Snapshot.EndTime.TotalSeconds));
            Assert.Equal(Start.AddSeconds(2), recent[0].ReceivedTime);
        }

        [Fact]
        public void Add_DropsReportsOlderThanFiveSeconds()
        {
            var buffer = new TimelineSnapshotBuffer();
            buffer.Add(Start, Report(1, 100));
            buffer.Add(Start.AddSeconds(1), Report(1, 100));

            buffer.Add(Start.AddSeconds(5.5), Report(2, 200));

            Assert.Equal(2, buffer.Count);
        }

        [Fact]
        public void GetRecent_DropsReportsOlderThanFiveSeconds()
        {
            var buffer = new TimelineSnapshotBuffer();
            buffer.Add(Start, Report(1, 100));

            Assert.Empty(buffer.GetRecent(Start.AddSeconds(6), TimeSpan.FromSeconds(10)));
            Assert.Equal(0, buffer.Count);
        }

        [Fact]
        public void Clear_ForgetsEverything()
        {
            var buffer = new TimelineSnapshotBuffer();
            buffer.Add(Start, Report(1, 100));

            buffer.Clear();

            Assert.Empty(buffer.GetRecent(Start, ReplayWindow));
        }

        [Fact]
        public void LengthReportedJustBeforeTheTitle_IsShownAfterReplay()
        {
            // Firefox: the new song's length (3:20) for ~60 ms, then 0/0, then the new title. The title change
            // forgets the old timeline, and replaying the last 3 s brings the length back
            var clock = new FakeTimeProvider(Start);
            var buffer = new TimelineSnapshotBuffer();
            var controller = new TimelineController(clock);
            controller.Update(Report(170, 180));

            var lengthReport = Report(0.1, 200) with { LastUpdatedTime = clock.GetUtcNow() };
            buffer.Add(clock.GetUtcNow(), lengthReport);
            controller.Update(lengthReport);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            buffer.Add(clock.GetUtcNow(), Empty);
            controller.Update(Empty);
            clock.Advance(TimeSpan.FromMilliseconds(200));

            controller.Clear();
            foreach (var (_, snapshot) in buffer.GetRecent(clock.GetUtcNow(), ReplayWindow))
            {
                controller.Update(snapshot);
            }
            controller.Update(Empty);

            Assert.True(controller.HasTimeline);
            Assert.Equal(TimeSpan.FromSeconds(200), controller.EndTime);
            Assert.Equal(TimeSpan.FromMilliseconds(100 + 260), controller.Position);
        }
    }
}
