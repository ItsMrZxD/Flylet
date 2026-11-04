using System;
using System.Collections.Generic;
using Flylet.Core.Helpers;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Flylet.Core.Tests
{
    public class BackgroundListTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider clock = new(Start);

        [Fact]
        public void Serialize_ThenParse_RoundTripsAllFieldsInOrder()
        {
            var original = new List<BackgroundItem>
            {
                new() { FileName = "one.jpg", Zoom = 150, PositionX = 20, PositionY = 80, Turns = 1 },
                new() { FileName = "two.gif", Zoom = 250, PositionX = 33, PositionY = 66, Turns = 3 },
                new() { FileName = "three.jpg", Zoom = 100, PositionX = 0, PositionY = 100, Turns = 0 },
            };

            string json = BackgroundList.Serialize(original);
            var parsed = BackgroundList.Parse(json);

            Assert.Equal(original.Count, parsed.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].FileName, parsed[i].FileName);
                Assert.Equal(original[i].Zoom, parsed[i].Zoom);
                Assert.Equal(original[i].PositionX, parsed[i].PositionX);
                Assert.Equal(original[i].PositionY, parsed[i].PositionY);
                Assert.Equal(original[i].Turns, parsed[i].Turns);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        public void Parse_ReturnsEmptyListForBadInput(string text)
        {
            var result = BackgroundList.Parse(text);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_DropsEntriesWithNullBlankOrFolderFileName()
        {
            string json = @"[
                { ""FileName"": null, ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": """", ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": ""   "", ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": ""..\\evil.jpg"", ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": ""a/b.jpg"", ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": ""good.jpg"", ""Zoom"": 100, ""Turns"": 0 }
            ]";

            var result = BackgroundList.Parse(json);

            Assert.Single(result);
            Assert.Equal("good.jpg", result[0].FileName);
        }

        [Fact]
        public void Parse_KeepsFirstDuplicateIgnoringCase()
        {
            string json = @"[
                { ""FileName"": ""photo.jpg"", ""Zoom"": 100, ""Turns"": 0 },
                { ""FileName"": ""PHOTO.JPG"", ""Zoom"": 200, ""Turns"": 2 },
                { ""FileName"": ""other.jpg"", ""Zoom"": 150, ""Turns"": 1 }
            ]";

            var result = BackgroundList.Parse(json);

            Assert.Equal(2, result.Count);
            Assert.Equal("photo.jpg", result[0].FileName);
            Assert.Equal(100, result[0].Zoom);
            Assert.Equal(0, result[0].Turns);
            Assert.Equal("other.jpg", result[1].FileName);
        }

        [Fact]
        public void Parse_ClampsFramingIntoRange()
        {
            string json = @"[
                { ""FileName"": ""low.jpg"", ""Zoom"": 50, ""PositionX"": -10, ""PositionY"": -50 },
                { ""FileName"": ""high.jpg"", ""Zoom"": 999, ""PositionX"": 200, ""PositionY"": 500 }
            ]";

            var result = BackgroundList.Parse(json);

            Assert.Equal(2, result.Count);
            Assert.Equal(100, result[0].Zoom);
            Assert.Equal(0, result[0].PositionX);
            Assert.Equal(0, result[0].PositionY);
            Assert.Equal(300, result[1].Zoom);
            Assert.Equal(100, result[1].PositionX);
            Assert.Equal(100, result[1].PositionY);
        }

        [Fact]
        public void Parse_NormalizesTurnsTo0Through3()
        {
            string json = @"[
                { ""FileName"": ""five.jpg"", ""Turns"": 5 },
                { ""FileName"": ""neg.jpg"", ""Turns"": -1 },
                { ""FileName"": ""zero.jpg"", ""Turns"": 0 },
                { ""FileName"": ""three.jpg"", ""Turns"": 3 },
                { ""FileName"": ""eight.jpg"", ""Turns"": 8 }
            ]";

            var result = BackgroundList.Parse(json);

            Assert.Equal(1, result[0].Turns); // 5 -> 1
            Assert.Equal(3, result[1].Turns); // -1 -> 3
            Assert.Equal(0, result[2].Turns); // 0 -> 0
            Assert.Equal(3, result[3].Turns); // 3 -> 3
            Assert.Equal(0, result[4].Turns); // 8 -> 0
        }

        [Theory]
        [InlineData("a.gif", true)]
        [InlineData("a.GIF", true)]
        [InlineData("a.JPG", false)]
        public void IsAnimated_ReturnsTrueOnlyForGif(string fileName, bool expected)
        {
            var item = new BackgroundItem { FileName = fileName };

            Assert.Equal(expected, item.IsAnimated);
        }

        [Fact]
        public void Select_MakesItemCurrent_WhenInList()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });

            rotation.Select("b");

            Assert.Equal("b", rotation.Current);
        }

        [Fact]
        public void Select_DoesNothing_WhenNotInList()
        {
            var rotation = new BackgroundRotation(clock);
            rotation.SetItems(new[] { "a", "b", "c" });

            rotation.Select("z");

            Assert.Equal("a", rotation.Current);
        }

        [Fact]
        public void Select_ThenAdvanceIfDue_WaitsForInterval()
        {
            var rotation = new BackgroundRotation(clock)
            {
                Interval = TimeSpan.FromMinutes(5),
            };
            rotation.SetItems(new[] { "a", "b", "c" });

            rotation.Select("c");
            Assert.Equal("c", rotation.Current);

            clock.Advance(TimeSpan.FromMinutes(4));
            Assert.False(rotation.AdvanceIfDue());
            Assert.Equal("c", rotation.Current);

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.True(rotation.AdvanceIfDue());
            Assert.Equal("a", rotation.Current);
        }
    }
}
