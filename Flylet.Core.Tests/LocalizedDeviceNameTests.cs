using Flylet.Core.Display;
using Xunit;

namespace Flylet.Core.Tests
{
    public class LocalizedDeviceNameTests
    {
        [Theory]
        [InlineData(@"\\?\DISPLAY#MSI5CD0#7&4478184&0&UID268#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", @"DISPLAY\MSI5CD0\7&4478184&0&UID268")]
        [InlineData(@"\\?\DISPLAY#GSM5830#1&8713bca&0&UID0", @"DISPLAY\GSM5830\1&8713bca&0&UID0")]
        [InlineData(@"DISPLAY", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ToInstanceId_ConvertsInterfacePath(string path, string expected)
        {
            Assert.Equal(expected, LocalizedDeviceName.ToInstanceId(path));
        }

        [Fact]
        public void Get_FallsBackWhenTheDeviceIsUnknown()
        {
            Assert.Equal("Generic PnP Monitor", LocalizedDeviceName.Get(@"\\?\DISPLAY#NOPE0000#0#{x}", "Generic PnP Monitor"));
            Assert.Equal("Display", LocalizedDeviceName.Get(null, "Display"));
        }
    }
}
