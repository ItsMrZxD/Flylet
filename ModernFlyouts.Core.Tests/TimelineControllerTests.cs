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
    }
}
