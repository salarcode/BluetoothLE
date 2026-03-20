using Salar.BluetoothLE.Core.Models;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class ConnectionConfigTests
{
    [Fact]
    public void Default_AutoConnectIsFalse()
    {
        Assert.False(ConnectionConfig.Default.AutoConnect);
    }

    [Fact]
    public void Default_RequestMtuIsNull()
    {
        Assert.Null(ConnectionConfig.Default.RequestMtu);
    }

    [Fact]
    public void Default_ConnectionTimeoutIs30Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ConnectionConfig.Default.ConnectionTimeout);
    }

    [Fact]
    public void CanCreate_WithAutoConnect()
    {
        var config = new ConnectionConfig { AutoConnect = true };
        Assert.True(config.AutoConnect);
    }

    [Fact]
    public void CanCreate_WithRequestMtu()
    {
        var config = new ConnectionConfig { RequestMtu = 512 };
        Assert.Equal(512, config.RequestMtu);
    }

    [Fact]
    public void CanCreate_WithCustomTimeout()
    {
        var config = new ConnectionConfig { ConnectionTimeout = TimeSpan.FromSeconds(60) };
        Assert.Equal(TimeSpan.FromSeconds(60), config.ConnectionTimeout);
    }

    [Fact]
    public void Default_IsSameInstanceEachTime()
    {
        Assert.Same(ConnectionConfig.Default, ConnectionConfig.Default);
    }
}
