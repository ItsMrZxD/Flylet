using Flylet.Core.Audio;
using Xunit;

namespace Flylet.Core.Tests
{
    public class AppAudioNamingTests
    {
        [Fact]
        public void GroupKey_SameProgramFile_IsOneRow()
        {
            Assert.Equal(
                AppAudioNaming.GroupKey(false, @"C:\Program Files\Google\Chrome\chrome.exe", 10),
                AppAudioNaming.GroupKey(false, @"c:\program files\google\chrome\CHROME.EXE", 22));
        }

        [Fact]
        public void GroupKey_NoPath_UsesTheProcess_AndSystemSoundsIsItsOwn()
        {
            Assert.NotEqual(AppAudioNaming.GroupKey(false, null, 10), AppAudioNaming.GroupKey(false, null, 11));
            Assert.Equal(AppAudioNaming.SystemSoundsKey, AppAudioNaming.GroupKey(true, @"C:\x.exe", 4));
        }

        [Theory]
        [InlineData("Spotify", "Spotify Music", @"C:\s.exe", null, "Spotify")]
        [InlineData("@%SystemRoot%\\System32\\AudioSrv.Dll,-202", "Mozilla Firefox", @"C:\f.exe", null, "Mozilla Firefox")]
        [InlineData(null, "  ", @"C:\Games\Tool\player.exe", null, "player")]
        [InlineData("", null, null, "vlc", "vlc")]
        [InlineData(null, null, null, null, "App 42")]
        public void PickName_FallsBackInOrder(string display, string description, string path, string process, string expected)
        {
            Assert.Equal(expected, AppAudioNaming.PickName(display, description, path, process, 42));
        }

        [Theory]
        [InlineData("spotify", "S")]
        [InlineData("  (beta) player", "B")]
        [InlineData("7-Zip", "7")]
        [InlineData("", "?")]
        [InlineData(null, "?")]
        [InlineData("---", "?")]
        public void InitialOf_TakesTheFirstLetterOrDigit(string name, string expected)
        {
            Assert.Equal(expected, AppAudioNaming.InitialOf(name));
        }

        [Theory]
        [InlineData("Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", true)]
        [InlineData("Google Chrome", "Chrome", true)]
        [InlineData("Chrome", "Chrome", false)]
        [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Steam\steam.exe", @"{6D809377-6AF0-444B-8957-A3773F02200E}\Steam\steam.exe", false)]
        [InlineData("steam.exe", "x", false)]
        [InlineData("  ", "x", false)]
        [InlineData(null, "x", false)]
        public void IsUsableStartMenuName_RejectsRawIds(string name, string appId, bool expected)
        {
            Assert.Equal(expected, AppAudioNaming.IsUsableStartMenuName(name, appId));
        }

        [Fact]
        public void Compare_PutsSystemSoundsLast()
        {
            Assert.True(AppAudioNaming.Compare(true, "A", false, "Z") > 0);
            Assert.True(AppAudioNaming.Compare(false, "chrome", false, "Spotify") < 0);
        }
    }
}
