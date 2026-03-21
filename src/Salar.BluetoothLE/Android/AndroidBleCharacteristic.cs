using Android.Bluetooth;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Android;

/// <summary>
/// Implements a BLE characteristic wrapper for Android GATT operations.
/// </summary>
public class AndroidBleCharacteristic : IBleCharacteristic
{
    private readonly BluetoothGattCharacteristic _characteristic;
    private readonly AndroidBleDevice _device;
    private Action<byte[]>? _notificationHandler;

    /// <summary>
    /// Initializes a new AndroidBleCharacteristic instance.
    /// </summary>
    public AndroidBleCharacteristic(BluetoothGattCharacteristic characteristic, AndroidBleDevice device)
    {
        _characteristic = characteristic;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_characteristic.Uuid!.ToString()!);

    public bool CanRead => (_characteristic.Properties & GattProperty.Read) != 0;
    public bool CanWrite => (_characteristic.Properties & GattProperty.Write) != 0;
    public bool CanWriteWithoutResponse => (_characteristic.Properties & GattProperty.WriteNoResponse) != 0;
    public bool CanNotify => (_characteristic.Properties & GattProperty.Notify) != 0;
    public bool CanIndicate => (_characteristic.Properties & GattProperty.Indicate) != 0;

    /// <summary>
    /// Reads the current value of the characteristic.
    /// </summary>
    public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead)
            throw new BleException(BleErrorCode.OperationFailed, "Characteristic does not support read.");
        return _device.ReadCharacteristicAsync(_characteristic, cancellationToken);
    }

    /// <summary>
    /// Writes data to the characteristic using the specified write mode.
    /// </summary>
    public Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
    {
        return _device.WriteCharacteristicAsync(_characteristic, data, writeType, cancellationToken);
    }

    /// <summary>
    /// Starts notifications for the characteristic and registers a handler.
    /// </summary>
    public Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
    {
        _notificationHandler = handler;
        return _device.SetCharacteristicNotificationAsync(_characteristic, true, handler, cancellationToken);
    }

    /// <summary>
    /// Stops notifications for the characteristic.
    /// </summary>
    public Task StopNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _notificationHandler = null;
        return _device.SetCharacteristicNotificationAsync(_characteristic, false, null, cancellationToken);
    }
}
