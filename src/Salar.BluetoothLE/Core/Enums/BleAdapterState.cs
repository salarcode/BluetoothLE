namespace Salar.BluetoothLE.Core.Enums;

/// <summary>
/// Defines the power and availability state of the local BLE adapter.
/// </summary>
public enum BleAdapterState
{
    Unknown,
    Unavailable,
    Unauthorized,
    PoweredOff,
    PoweredOn,
    Resetting
}
