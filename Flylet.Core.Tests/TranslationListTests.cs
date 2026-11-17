using System;
using System.IO;
using Flylet.Core.Helpers;
using Xunit;

namespace Flylet.Core.Tests
{
    public class TranslationListTests : IDisposable
    {
        private const string TranslationFile = "Flylet.resources.dll";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "FlyletTests_" + Guid.NewGuid().ToString("N"));

        public TranslationListTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        private void AddFolder(string name, string file = TranslationFile)
        {
            string path = Path.Combine(folder, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, file), string.Empty);
        }

        [Fact]
        public void Find_ListsTranslationFoldersAndTheBuiltInLanguage_ByName()
        {
            foreach (string name in new[] { "vi", "de", "zh-Hant", "af", "en-GB", "da-DK", "zh-Hans", "nb" })
            {
                AddFolder(name);
            }

            var names = TranslationList.Find(folder, TranslationFile, "en-US");

            Assert.Equal(new[] { "af", "da-DK", "de", "en-GB", "en-US", "nb", "vi", "zh-Hans", "zh-Hant" }, names);
        }

        [Fact]
        public void Find_FolderWithoutTheAppsTranslation_IsLeftOut()
        {
            AddFolder("de");
            // Other libraries bring language folders of their own
            AddFolder("cs-CZ", "ModernWpf.resources.dll");
            Directory.CreateDirectory(Path.Combine(folder, "Assets"));

            var names = TranslationList.Find(folder, TranslationFile, "en-US");

            Assert.Equal(new[] { "de", "en-US" }, names);
        }

        [Fact]
        public void Find_BuiltInLanguageWithAFolderToo_IsListedOnce()
        {
            AddFolder("en-US");
            AddFolder("fr");

            var names = TranslationList.Find(folder, TranslationFile, "en-US");

            Assert.Equal(new[] { "en-US", "fr" }, names);
        }

        [Fact]
        public void Find_OldShortChineseNames_AreLeftOutAsBefore()
        {
            foreach (string name in new[] { "zh-CN", "zh-TW", "zh-Hans", "zh-Hant" })
            {
                AddFolder(name);
            }

            var names = TranslationList.Find(folder, TranslationFile, "en-US");

            Assert.Equal(new[] { "en-US", "zh-Hans", "zh-Hant" }, names);
        }

        [Fact]
        public void Find_SavedLanguage_StaysListedWhateverItsName()
        {
            foreach (string name in new[] { "zh-CN", "zh-TW", "zh-Hans" })
            {
                AddFolder(name);
            }

            var names = TranslationList.Find(folder, TranslationFile, "en-US", savedLanguage: "zh-CN");

            Assert.Equal(new[] { "en-US", "zh-CN", "zh-Hans" }, names);
        }

        [Fact]
        public void Find_SavedLanguageWithoutATranslation_IsNotAdded()
        {
            AddFolder("de");

            var names = TranslationList.Find(folder, TranslationFile, "en-US", savedLanguage: "sv");

            Assert.Equal(new[] { "de", "en-US" }, names);
        }

        [Fact]
        public void Find_MissingFolderOrNoBuiltInLanguage_DoesNotThrow()
        {
            Assert.Equal(new[] { "en-US" }, TranslationList.Find(Path.Combine(folder, "none"), TranslationFile, "en-US"));

            AddFolder("de");
            Assert.Equal(new[] { "de" }, TranslationList.Find(folder, TranslationFile, null));
        }
    }
}
