using System.Reflection;
using Salar.BluetoothLE.Linux;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class BleAdapterExtensionsTests
{
    [Fact]
    public void CreatePlatformAdapter_ReturnsLinuxAdapterOnLinux()
    {
        var createPlatformAdapter = typeof(BleAdapterExtensions).GetMethod(
            "CreatePlatformAdapter",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(createPlatformAdapter);

        var adapter = createPlatformAdapter!.Invoke(null, null);

        var typed = Assert.IsType<LinuxBleAdapter>(adapter);
        typed.Dispose();
    }
}
