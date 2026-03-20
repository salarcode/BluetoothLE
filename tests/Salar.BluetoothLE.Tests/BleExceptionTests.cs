using Salar.BluetoothLE.Core.Models;
using Xunit;

namespace Salar.BluetoothLE.Tests;

public class BleExceptionTests
{
    [Fact]
    public void Constructor_WithErrorCode_SetsErrorCode()
    {
        var ex = new BleException(BleErrorCode.NotSupported);
        Assert.Equal(BleErrorCode.NotSupported, ex.ErrorCode);
    }

    [Fact]
    public void Constructor_WithErrorCode_SetsDefaultMessage()
    {
        var ex = new BleException(BleErrorCode.NotSupported);
        Assert.Equal("BLE is not supported on this device.", ex.Message);
    }

    [Fact]
    public void Constructor_WithCustomMessage_SetsMessage()
    {
        var ex = new BleException(BleErrorCode.ConnectionFailed, "Custom error");
        Assert.Equal("Custom error", ex.Message);
        Assert.Equal(BleErrorCode.ConnectionFailed, ex.ErrorCode);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new BleException(BleErrorCode.OperationFailed, "outer", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(BleErrorCode.OperationFailed, ex.ErrorCode);
    }

    [Theory]
    [InlineData(BleErrorCode.None, "No error.")]
    [InlineData(BleErrorCode.NotSupported, "BLE is not supported on this device.")]
    [InlineData(BleErrorCode.NotAuthorized, "BLE access is not authorized. Please grant permissions.")]
    [InlineData(BleErrorCode.NotPoweredOn, "Bluetooth adapter is not powered on.")]
    [InlineData(BleErrorCode.AlreadyConnected, "Device is already connected.")]
    [InlineData(BleErrorCode.NotConnected, "Device is not connected.")]
    [InlineData(BleErrorCode.ScanFailed, "BLE scan failed.")]
    [InlineData(BleErrorCode.ConnectionFailed, "Connection to BLE device failed.")]
    [InlineData(BleErrorCode.ConnectionTimeout, "Connection to BLE device timed out.")]
    [InlineData(BleErrorCode.ServiceNotFound, "BLE service not found on device.")]
    [InlineData(BleErrorCode.CharacteristicNotFound, "BLE characteristic not found on service.")]
    [InlineData(BleErrorCode.OperationFailed, "BLE operation failed.")]
    [InlineData(BleErrorCode.Unknown, "An unknown BLE error occurred.")]
    public void DefaultMessages_AreCorrect(BleErrorCode code, string expectedMessage)
    {
        var ex = new BleException(code);
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void IsException_CanBeCaught()
    {
        void Throw() => throw new BleException(BleErrorCode.ConnectionTimeout);
        Assert.Throws<BleException>(Throw);
    }
}
