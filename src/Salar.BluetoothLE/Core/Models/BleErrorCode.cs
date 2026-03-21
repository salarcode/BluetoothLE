namespace Salar.BluetoothLE.Core.Models;

/// <summary>
/// Defines error codes returned by BLE operations.
/// </summary>
public enum BleErrorCode
{
    None,
    NotSupported,
    NotAuthorized,
    NotPoweredOn,
    AlreadyConnected,
    NotConnected,
    ScanFailed,
    ConnectionFailed,
    ConnectionTimeout,
    ServiceNotFound,
    CharacteristicNotFound,
    OperationFailed,
    Unknown
}
