using System;
using Microsoft.Extensions.Time.Testing;
using ModernFlyouts.Core.Media.Control;
using Xunit;

namespace ModernFlyouts.Core.Tests
{
    public class TimelineControllerTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Start);

        private static TimelineSnapshot Playing(double positionSeconds, double endSeconds = 180, double? rate = 1.0, DateTimeOffset? lastUpdated = null) => new()
        {
            EndTime = TimeSpan.FromSeconds(endSeconds),
            Position = TimeSpan.FromSeconds(positionSeconds),
            LastUpdatedTime = lastUpdated ?? Start,
            PlaybackRate = rate,
            IsPlaying = true
        };

        [Fact]
        public void Position_AdvancesWhilePlaying()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10));

            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(15), controller.Position);
        }

        [Fact]
        public void Position_CountsFromWhenTheSourceMeasuredIt()
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            var controller = new TimelineController(clock);

            controller.Update(Playing(10, lastUpdated: Start));

            Assert.Equal(TimeSpan.FromSeconds(12), controller.Position);
        }

        [Fact]
        public void Position_StopsAtEndTime()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(178));

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(TimeSpan.FromSeconds(180), controller.Position);
        }

        [Fact]
        public void Position_KeepsGoingWhenLengthIsUnknown()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(30, endSeconds: 0));

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(TimeSpan.FromSeconds(40), controller.Position);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        public void Position_UsesNormalSpeedWhenRateIsMissing(double? rate)
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10, rate: rate));

            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(15), controller.Position);
        }

        [Fact]
        public void Position_FollowsPlaybackRate()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10, rate: 2.0));

            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(20), controller.Position);
        }

        [Fact]
        public void Position_DoesNotAdvanceBeforeAFutureUpdateTime()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10, lastUpdated: Start.AddSeconds(30)));

            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(10), controller.Position);
        }

        [Fact]
        public void Position_CountsFromNowWhenUpdateTimeIsMissing()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10) with { LastUpdatedTime = default });

            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.Equal(TimeSpan.FromSeconds(15), controller.Position);
        }

        [Fact]
        public void Position_HoldsWhilePaused()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10) with { IsPlaying = false });

            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(TimeSpan.FromSeconds(10), controller.Position);
        }

        [Fact]
        public void Pause_WithoutNewPosition_FreezesAtTheEstimate()
        {
            var controller = new TimelineController(clock);
            var playing = Playing(10);
            controller.Update(playing);
            clock.Advance(TimeSpan.FromSeconds(5));

            controller.Update(playing with { IsPlaying = false });
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.Equal(TimeSpan.FromSeconds(15), controller.Position);
        }

        [Fact]
        public void NewPosition_ReplacesTheEstimate()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(10));
            clock.Advance(TimeSpan.FromSeconds(5));

            controller.Update(Playing(60, lastUpdated: clock.GetUtcNow()));

            Assert.Equal(TimeSpan.FromSeconds(60), controller.Position);
        }

        [Fact]
        public void Timer_RunsOnlyWhilePlaying()
        {
            var controller = new TimelineController(clock);

            controller.Update(Playing(10));
            Assert.True(controller.IsTicking);

            controller.Update(Playing(12) with { IsPlaying = false });
            Assert.False(controller.IsTicking);

            controller.Update(Playing(12));
            controller.Dispose();
            Assert.False(controller.IsTicking);
        }

        // The sequences below replay what Firefox reported through NPSM and GSMTC (captured 2026-09-11)

        [Fact]
        public void EmptyTimelineWhilePlaying_KeepsCountingFromTheLastRealOne()
        {
            // Paused at 0:44.711 of 3:16, resumed 12 s later, then 14 s after that Firefox
            // reported position 0 and length 0 while still playing
            var controller = new TimelineController(clock);
            var length = TimeSpan.FromMilliseconds(196_001);
            controller.Update(new TimelineSnapshot { EndTime = length, Position = TimeSpan.FromMilliseconds(44_711), LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 0, IsPlaying = false });
            clock.Advance(TimeSpan.FromSeconds(12));
            controller.Update(new TimelineSnapshot { EndTime = length, Position = TimeSpan.FromMilliseconds(44_711), LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            clock.Advance(TimeSpan.FromSeconds(14));

            controller.Update(new TimelineSnapshot { LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            clock.Advance(TimeSpan.FromSeconds(5));

            Assert.True(controller.HasTimeline);
            Assert.True(controller.IsTicking);
            Assert.Equal(length, controller.EndTime);
            Assert.Equal(TimeSpan.FromMilliseconds(44_711 + 19_000), controller.Position);
        }

        [Fact]
        public void NewTrack_ShowsNothingUntilARealTimelineArrives_ThenKeepsIt()
        {
            // A new video first reported 0/0, then its real start and length (3:52), then 0/0 again 0.3 s later
            var controller = new TimelineController(clock);
            controller.Update(new TimelineSnapshot { LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            Assert.False(controller.HasTimeline);
            Assert.False(controller.IsTicking);

            clock.Advance(TimeSpan.FromMilliseconds(500));
            controller.Update(new TimelineSnapshot { EndTime = TimeSpan.FromSeconds(232), Position = TimeSpan.FromMilliseconds(177), LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            clock.Advance(TimeSpan.FromMilliseconds(300));
            controller.Update(new TimelineSnapshot { LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.True(controller.HasTimeline);
            Assert.Equal(TimeSpan.FromSeconds(232), controller.EndTime);
            Assert.Equal(TimeSpan.FromMilliseconds(177 + 300 + 10_000), controller.Position);
        }

        [Fact]
        public void Pause_ReportsTheRealPositionAfterEmptyTimelines()
        {
            // While playing, only 0/0 arrived; the pause brought the real position 0:30.992 of 3:52
            var controller = new TimelineController(clock);
            controller.Update(Playing(5, endSeconds: 232));
            clock.Advance(TimeSpan.FromSeconds(1));
            controller.Update(new TimelineSnapshot { LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });
            clock.Advance(TimeSpan.FromSeconds(24));

            controller.Update(new TimelineSnapshot { EndTime = TimeSpan.FromSeconds(232), Position = TimeSpan.FromMilliseconds(30_992), LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 0, IsPlaying = false });
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.False(controller.IsTicking);
            Assert.Equal(TimeSpan.FromMilliseconds(30_992), controller.Position);
        }

        [Fact]
        public void Clear_ForgetsTheOldTrack()
        {
            var controller = new TimelineController(clock);
            controller.Update(Playing(100));

            controller.Clear();
            controller.Update(new TimelineSnapshot { LastUpdatedTime = clock.GetUtcNow(), PlaybackRate = 1, IsPlaying = true });

            Assert.False(controller.HasTimeline);
            Assert.False(controller.IsTicking);
            Assert.Equal(TimeSpan.Zero, controller.Position);
        }
    }
}
