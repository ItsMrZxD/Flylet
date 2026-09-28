using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Flylet.Core.Tests
{
    public class ResourceCompletenessTests
    {
        private static string FindPropertiesDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Flylet", "Properties");
                if (File.Exists(Path.Combine(candidate, "Strings.resx")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Flylet/Properties/Strings.resx not found above the test output folder.");
        }

        private static HashSet<string> Keys(string path) =>
            XDocument.Load(path).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

        [Fact]
        public void EveryLanguageTranslatesEveryString()
        {
            // A missing key silently falls back to English, which shows up as a half-translated UI.
            var directory = FindPropertiesDirectory();
            var baseKeys = Keys(Path.Combine(directory, "Strings.resx"));

            var missing = Directory.GetFiles(directory, "Strings.*.resx")
                .Select(file => (Language: Path.GetFileNameWithoutExtension(file)["Strings.".Length..], Keys: Keys(file)))
                .Select(l => (l.Language, Missing: baseKeys.Except(l.Keys).OrderBy(k => k).ToList()))
                .Where(l => l.Missing.Count > 0)
                .Select(l => $"{l.Language}: {string.Join(", ", l.Missing)}")
                .ToList();

            Assert.True(missing.Count == 0, "Untranslated strings:\n" + string.Join("\n", missing));
        }
    }
}
