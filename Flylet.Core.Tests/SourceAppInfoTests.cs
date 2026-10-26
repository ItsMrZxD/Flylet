using System.Diagnostics;
using Flylet.Core.AppInformation;
using Flylet.Core.Interop;
using Xunit;

namespace Flylet.Core.Tests
{
    // GitHub issue #1: a media app running as administrator crashed Flylet, because
    // Process.Handle asks for full access and Windows denies it. The System process (PID 4)
    // denies that access too, so it stands in for an elevated app here.
    public class SourceAppInfoTests
    {
        private const uint SystemProcessId = 4;

        [Fact]
        public void FromProcessId_ProcessThatDeniesAccess_DoesNotThrow()
        {
            using var info = SourceAppInfo.FromProcessId(SystemProcessId);

            Assert.NotNull(info);
        }

        [Fact]
        public void IsImmersiveProcess_ProcessThatDeniesAccess_ReturnsFalse()
        {
            Assert.False(NativeMethods.IsImmersiveProcess(SystemProcessId));
        }

        [Fact]
        public void GetProcessImagePath_CurrentProcess_ReturnsExePath()
        {
            using var process = Process.GetCurrentProcess();

            Assert.Equal(process.MainModule.FileName, NativeMethods.GetProcessImagePath((uint)process.Id), ignoreCase: true);
        }
    }
}
