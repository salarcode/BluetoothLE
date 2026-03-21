namespace Salar.BluetoothLE.Core.Models;

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
