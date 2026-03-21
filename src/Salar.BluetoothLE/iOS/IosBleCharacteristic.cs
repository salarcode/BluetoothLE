using CoreBluetooth;
using Foundation;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.iOS;

/// <summary>
/// Implements a BLE characteristic wrapper for iOS CoreBluetooth operations.
/// </summary>
public class IosBleCharacteristic : IBleCharacteristic
{
    private readonly CBCharacteristic _characteristic;
    private readonly IosBleDevice _device;

    /// <summary>
    /// Initializes a new IosBleCharacteristic instance.
    /// </summary>
    public IosBleCharacteristic(CBCharacteristic characteristic, IosBleDevice device)
    {
        _characteristic = characteristic;
        _device = device;
    }

    public Guid Uuid => Guid.Parse(_characteristic.UUID.ToString());

    public bool CanRead => _characteristic.Properties.HasFlag(CBCharacteristicProperties.Read);
    public bool CanWrite => _characteristic.Properties.HasFlag(CBCharacteristicProperties.Write);
    public bool CanWriteWithoutResponse => _characteristic.Properties.HasFlag(CBCharacteristicProperties.WriteWithoutResponse);
    public bool CanNotify => _characteristic.Properties.HasFlag(CBCharacteristicProperties.Notify);
    public bool CanIndicate => _characteristic.Properties.HasFlag(CBCharacteristicProperties.Indicate);

    /// <summary>
    /// Reads the current value of the characteristic.
    /// </summary>
    public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
        => _device.ReadCharacteristicAsync(_characteristic, cancellationToken);

    /// <summary>
    /// Writes data to the characteristic using the specified write mode.
    /// </summary>
    public Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
        => _device.WriteCharacteristicAsync(_characteristic, data, writeType, cancellationToken);

    /// <summary>
    /// Starts notifications for the characteristic and registers a handler.
    /// </summary>
    public Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
        => _device.SetNotificationAsync(_characteristic, true, handler, cancellationToken);

    /// <summary>
    /// Stops notifications for the characteristic.
    /// </summary>
    public Task StopNotificationsAsync(CancellationToken cancellationToken = default)
        => _device.SetNotificationAsync(_characteristic, false, null, cancellationToken);
}
