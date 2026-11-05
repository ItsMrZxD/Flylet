using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class BackgroundRotationTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Start);

        [Fact]
        public void Empty_List_HasNoCurrentAndNeverChanges()
        {
            var rotation = new BackgroundRotation(clock);

            Assert.Null(rotation.Current);

            clock.Advance(TimeSpan.FromMinutes(10));
            Assert.False(rotation.AdvanceIfDue());

            rotation.Next();
            Assert.Null(rotation.Current);
        }

        [Fact]
        public void RestoredChangeTime_CountsTimeFromBeforeTheRestart()
        {
            var rotation = new BackgroundRotation(clock)
            {
                Interval = TimeSpan.FromDays(1)
            };
            rotation.SetItems(new[] { "a", "b" });
            rotation.LastChangeTime = Start - TimeSpan.FromDays(2);

            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("b", rotation.Current);
            Assert.Equal(Start, rotation.LastChangeTime);
        }

        [Fact]
        public void One_Item_NeverChanges()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "only" });

            Assert.Equal("only", rotation.Current);

            clock.Advance(TimeSpan.FromHours(1));
            Assert.False(rotation.AdvanceIfDue());
            rotation.Next();

            Assert.Equal("only", rotation.Current);
        }

        [Fact]
        public void InOrder_MovesThroughTheListAndWraps()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });

            Assert.Equal("a", rotation.Current);
            rotation.Next();
            Assert.Equal("b", rotation.Current);
            rotation.Next();
            Assert.Equal("c", rotation.Current);
            rotation.Next();
            Assert.Equal("a", rotation.Current);
        }

        [Fact]
        public void Interval_ChangesOnlyAfterItHasPassed()
        {
            var rotation = new BackgroundRotation(clock)
            {
                Interval = TimeSpan.FromMinutes(5),
            };
            rotation.SetItems(new[] { "a", "b", "c" });

            clock.Advance(TimeSpan.FromMinutes(4));
            Assert.False(rotation.AdvanceIfDue());
            Assert.Equal("a", rotation.Current);

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("b", rotation.Current);
        }

        [Fact]
        public void IntervalZero_ChangesOnEveryFlyoutOpen()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });

            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("b", rotation.Current);
            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("c", rotation.Current);
            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("a", rotation.Current);
        }

        [Fact]
        public void Shuffle_NeverRepeatsTwiceInARowAndShowsEveryItem()
        {
            var rotation = new BackgroundRotation(clock, new Random(42))
            {
                Order = BackgroundRotationOrder.Shuffle,
            };
            rotation.SetItems(new[] { "a", "b", "c", "d", "e" });

            var seen = new HashSet<string>();
            string previous = rotation.Current;
            seen.Add(previous);

            for (int i = 0; i < 200; i++)
            {
                rotation.Next();
                Assert.NotEqual(previous, rotation.Current);
                previous = rotation.Current;
                seen.Add(previous);
            }

            Assert.Equal(5, seen.Count);
        }

        [Fact]
        public void SetItems_KeepsCurrentWhenStillPresent()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });
            rotation.SetItems(new[] { "a", "b", "c", "d" });

            Assert.Equal("a", rotation.Current);
        }

        [Fact]
        public void SetItems_SwitchesToFirstWhenCurrentRemoved()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });
            rotation.SetItems(new[] { "x", "y" });

            Assert.Equal("x", rotation.Current);
        }

        [Fact]
        public void SetItems_Empty_MakesCurrentNull()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b" });
            rotation.SetItems(Array.Empty<string>());

            Assert.Null(rotation.Current);
        }

        [Fact]
        public void SetItems_IgnoresDuplicatesAndBlankEntries()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "", "b", "a", null, "b" });

            Assert.Equal(new[] { "a", "b" }, rotation.Items.ToArray());
            Assert.Equal("a", rotation.Current);
        }

        [Fact]
        public void SetItems_ResetsTheLastChangeTime()
        {
            var rotation = new BackgroundRotation(clock)
            {
                Interval = TimeSpan.FromMinutes(5),
            };
            rotation.SetItems(new[] { "a", "b" });

            clock.Advance(TimeSpan.FromMinutes(10));
            rotation.SetItems(new[] { "a", "b" });

            // Without the reset, the 10 minutes already elapsed would have allowed a change.
            Assert.False(rotation.AdvanceIfDue());
            Assert.Equal("a", rotation.Current);
        }
    }
}
