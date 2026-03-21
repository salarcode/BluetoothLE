namespace Salar.BluetoothLE.Core.Models;

public class BleException : Exception
{
    public BleErrorCode ErrorCode { get; }

    public BleException(BleErrorCode errorCode)
        : base(GetDefaultMessage(errorCode))
    {
        ErrorCode = errorCode;
    }

    public BleException(BleErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public BleException(BleErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    private static string GetDefaultMessage(BleErrorCode errorCode) => errorCode switch
    {
        BleErrorCode.None => "No error.",
        BleErrorCode.NotSupported => "BLE is not supported on this device.",
        BleErrorCode.NotAuthorized => "BLE access is not authorized. Please grant permissions.",
        BleErrorCode.NotPoweredOn => "Bluetooth adapter is not powered on.",
        BleErrorCode.AlreadyConnected => "Device is already connected.",
        BleErrorCode.NotConnected => "Device is not connected.",
        BleErrorCode.ScanFailed => "BLE scan failed.",
        BleErrorCode.ConnectionFailed => "Connection to BLE device failed.",
        BleErrorCode.ConnectionTimeout => "Connection to BLE device timed out.",
        BleErrorCode.ServiceNotFound => "BLE service not found on device.",
        BleErrorCode.CharacteristicNotFound => "BLE characteristic not found on service.",
        BleErrorCode.OperationFailed => "BLE operation failed.",
        _ => "An unknown BLE error occurred."
    };
}
