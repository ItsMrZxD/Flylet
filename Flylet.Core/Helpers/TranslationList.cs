using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Flylet.Core.Helpers
{
    /// <summary>
    /// Finds the languages the app is translated into by looking at its folder.
    /// </summary>
    /// <remarks>
    /// The list used to be made by asking for the app's texts in every culture Windows knows, about 850 of
    /// them. .NET keeps what it loads about a culture for good: that cost about 1 MB and 850 looks for a
    /// translation file at every start.
    /// </remarks>
    public static class TranslationList
    {
        /// <summary>
        /// The culture names of the app's translations, in the order the language list shows them (by name).
        /// </summary>
        /// <param name="appFolder">The app's folder: each translation is a file in a folder named after its culture.</param>
        /// <param name="translationFileName">The file every translation has, e.g. Flylet.resources.dll.</param>
        /// <param name="builtInLanguage">The language of the texts in the app itself, which has no folder; null for none.</param>
        /// <param name="savedLanguage">The language the user picked, which stays listed whatever its name; null for none.</param>
        public static IReadOnlyList<string> Find(string appFolder, string translationFileName, string builtInLanguage, string savedLanguage = null)
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(builtInLanguage))
            {
                names.Add(builtInLanguage);
            }

            try
            {
                foreach (string folder in Directory.EnumerateDirectories(appFolder))
                {
                    if (File.Exists(Path.Combine(folder, translationFileName)))
                    {
                        names.Add(Path.GetFileName(folder));
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // No folder to look in: only the built-in language
            }

            var listed = new List<string>(names.Count);
            foreach (string name in names)
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(name);
                    if (IsListedCulture(culture) || string.Equals(name, savedLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        listed.Add(culture.Name);
                    }
                }
                catch (CultureNotFoundException)
                {
                }
            }

            return listed;
        }

        /// <summary>
        /// Whether .NET's own list of cultures has this one under this name. It has zh-Hans-CN and zh-Hant-TW
        /// but not their old short names zh-CN and zh-TW, so those two translations never showed in the
        /// language list (zh-Hans and zh-Hant do). A culture's own name starts with its parent's: da-DK is
        /// under da, but zh-CN is under zh-Hans.
        /// </summary>
        private static bool IsListedCulture(CultureInfo culture)
        {
            return culture.IsNeutralCulture
                || culture.Name.StartsWith(culture.Parent.Name + "-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
