using System.Reflection;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class BleAdapterExtensionsTests
{
#if  !WINDOWS && !MACOS && !IOS && !ANDROID
    [Fact]
    public void CreatePlatformAdapter_ReturnsLinuxAdapterOnLinux()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var createPlatformAdapter = typeof(BleAdapterExtensions).GetMethod(
            "CreatePlatformAdapter",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(createPlatformAdapter);

        var adapter = createPlatformAdapter!.Invoke(null, null);

        var typed = Assert.IsType<Salar.BluetoothLE.Linux.LinuxBleAdapter>(adapter);
        typed.Dispose();
    }
#endif
}
