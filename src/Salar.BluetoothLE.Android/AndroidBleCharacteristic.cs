using Android.Bluetooth;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Android;

public class AndroidBleCharacteristic : IBleCharacteristic
{
    private readonly BluetoothGattCharacteristic _characteristic;
    private readonly AndroidBleDevice _device;
    private Action<byte[]>? _notificationHandler;

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

    public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead)
            throw new BleException(BleErrorCode.OperationFailed, "Characteristic does not support read.");
        return _device.ReadCharacteristicAsync(_characteristic, cancellationToken);
    }

    public Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
    {
        return _device.WriteCharacteristicAsync(_characteristic, data, writeType, cancellationToken);
    }

    public Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
    {
        _notificationHandler = handler;
        return _device.SetCharacteristicNotificationAsync(_characteristic, true, handler, cancellationToken);
    }

    public Task StopNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _notificationHandler = null;
        return _device.SetCharacteristicNotificationAsync(_characteristic, false, null, cancellationToken);
    }
}
