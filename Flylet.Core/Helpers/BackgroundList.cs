using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// One photo or GIF in the flyout background list, with how the user framed it.
    /// </summary>
    public sealed class BackgroundItem
    {
        /// <summary>
        /// The imported copy's file name, inside the backgrounds folder.
        /// </summary>
        public string FileName { get; set; }

        public double Zoom { get; set; } = 100;

        public double PositionX { get; set; } = 50;

        public double PositionY { get; set; } = 50;

        /// <summary>
        /// Quarter turns for a GIF. A still photo is turned in its file instead.
        /// </summary>
        public int Turns { get; set; }

        /// <summary>
        /// SHA-256 of the file the user picked, so picking it again can be skipped. Lists saved before 0.12.1
        /// don't have it.
        /// </summary>
        public string SourceHash { get; set; }

        public bool IsAnimated => string.Equals(Path.GetExtension(FileName), ".gif", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Saves the background list as text. It goes in a file, not in the app's settings, which cap a value at 8 KB.
    /// </summary>
    public static class BackgroundList
    {
        public static string Serialize(IEnumerable<BackgroundItem> items) => JsonSerializer.Serialize(items);

        /// <summary>
        /// Reads a saved list. Never throws: damaged text gives an empty list, and entries that couldn't be
        /// a file in the backgrounds folder are dropped, as are repeats. Out-of-range framing is pulled back in.
        /// </summary>
        public static List<BackgroundItem> Parse(string text)
        {
            List<BackgroundItem> parsed;
            try
            {
                parsed = string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<List<BackgroundItem>>(text);
            }
            catch (JsonException)
            {
                parsed = null;
            }

            var items = new List<BackgroundItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in parsed ?? Enumerable.Empty<BackgroundItem>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FileName)
                    || item.FileName != Path.GetFileName(item.FileName)
                    || !seen.Add(item.FileName))
                {
                    continue;
                }

                item.Zoom = Math.Clamp(item.Zoom, 100, 300);
                item.PositionX = Math.Clamp(item.PositionX, 0, 100);
                item.PositionY = Math.Clamp(item.PositionY, 0, 100);
                item.Turns = ((item.Turns % 4) + 4) % 4;
                items.Add(item);
            }

            return items;
        }

        /// <summary>True if the text is a readable saved list, even an empty one ("[]").</summary>
        public static bool IsValidList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                return JsonSerializer.Deserialize<List<BackgroundItem>>(text) != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
