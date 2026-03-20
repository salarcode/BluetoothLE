using Salar.BluetoothLE.Core.Models;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class ScanResultTests
{
    [Fact]
    public void DefaultAddress_IsEmpty()
    {
        var result = new ScanResult();
        Assert.Equal(string.Empty, result.Address);
    }

    [Fact]
    public void DefaultName_IsNull()
    {
        var result = new ScanResult();
        Assert.Null(result.Name);
    }

    [Fact]
    public void DefaultServiceUuids_IsEmpty()
    {
        var result = new ScanResult();
        Assert.NotNull(result.ServiceUuids);
        Assert.Empty(result.ServiceUuids);
    }

    [Fact]
    public void DefaultManufacturerData_IsEmpty()
    {
        var result = new ScanResult();
        Assert.NotNull(result.ManufacturerData);
        Assert.Empty(result.ManufacturerData);
    }

    [Fact]
    public void CanSetName()
    {
        var result = new ScanResult { Name = "TestDevice" };
        Assert.Equal("TestDevice", result.Name);
    }

    [Fact]
    public void CanSetAddress()
    {
        var result = new ScanResult { Address = "AA:BB:CC:DD:EE:FF" };
        Assert.Equal("AA:BB:CC:DD:EE:FF", result.Address);
    }

    [Fact]
    public void CanSetRssi()
    {
        var result = new ScanResult { Rssi = -70 };
        Assert.Equal(-70, result.Rssi);
    }

    [Fact]
    public void CanSetIsConnectable()
    {
        var result = new ScanResult { IsConnectable = true };
        Assert.True(result.IsConnectable);
    }

    [Fact]
    public void ToString_WithName_ContainsName()
    {
        var result = new ScanResult { Name = "MyDevice", Address = "00:11:22:33:44:55", Rssi = -65, IsConnectable = true };
        var str = result.ToString();
        Assert.Contains("MyDevice", str);
        Assert.Contains("00:11:22:33:44:55", str);
    }

    [Fact]
    public void ToString_WithoutName_ContainsUnknown()
    {
        var result = new ScanResult { Address = "00:11:22:33:44:55" };
        Assert.Contains("(unknown)", result.ToString());
    }

    [Fact]
    public void Timestamp_IsRecentUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var result = new ScanResult();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(result.Timestamp, before, after);
    }

    [Fact]
    public void CanSetAdvertisementData()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = new ScanResult { AdvertisementData = data };
        Assert.Equal(data, result.AdvertisementData);
    }

    [Fact]
    public void CanSetServiceUuids()
    {
        var uuid = Guid.NewGuid();
        var result = new ScanResult { ServiceUuids = new List<Guid> { uuid } };
        Assert.Single(result.ServiceUuids);
        Assert.Equal(uuid, result.ServiceUuids[0]);
    }

    [Fact]
    public void CanSetManufacturerData()
    {
        var data = new byte[] { 0xAA, 0xBB };
        var result = new ScanResult { ManufacturerData = new Dictionary<ushort, byte[]> { { 0x004C, data } } };
        Assert.True(result.ManufacturerData.ContainsKey(0x004C));
        Assert.Equal(data, result.ManufacturerData[0x004C]);
    }
}
