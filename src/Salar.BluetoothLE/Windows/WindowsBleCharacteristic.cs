using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Salar.BluetoothLE.Core.Enums;
using Salar.BluetoothLE.Core.Interfaces;
using Salar.BluetoothLE.Core.Models;

namespace Salar.BluetoothLE.Windows;

public class WindowsBleCharacteristic : IBleCharacteristic
{
    private readonly GattCharacteristic _characteristic;
    private Action<byte[]>? _notificationHandler;
    private readonly object _handlerLock = new();

    public WindowsBleCharacteristic(GattCharacteristic characteristic)
    {
        _characteristic = characteristic;
    }

    public Guid Uuid => _characteristic.Uuid;

    public bool CanRead => _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read);
    public bool CanWrite => _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write);
    public bool CanWriteWithoutResponse => _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
    public bool CanNotify => _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify);
    public bool CanIndicate => _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate);

    public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead) throw new BleException(BleErrorCode.OperationFailed, "Characteristic does not support read.");
        var result = await _characteristic.ReadValueAsync();
        if (result.Status != GattCommunicationStatus.Success)
            throw new BleException(BleErrorCode.OperationFailed, $"Read failed: {result.Status}");
        return ReadBytes(result.Value);
    }

    public async Task WriteAsync(byte[] data, WriteType writeType = WriteType.WithResponse, CancellationToken cancellationToken = default)
    {
        var writer = new DataWriter();
        writer.WriteBytes(data);
        var buffer = writer.DetachBuffer();

        GattWriteOption option = writeType == WriteType.WithoutResponse
            ? GattWriteOption.WriteWithoutResponse
            : GattWriteOption.WriteWithResponse;

        var status = await _characteristic.WriteValueAsync(buffer, option);
        if (status != GattCommunicationStatus.Success)
            throw new BleException(BleErrorCode.OperationFailed, $"Write failed: {status}");
    }

    public async Task StartNotificationsAsync(Action<byte[]> handler, CancellationToken cancellationToken = default)
    {
        lock (_handlerLock) { _notificationHandler = handler; }
        _characteristic.ValueChanged += OnValueChanged;

        var cccdValue = CanIndicate
            ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
            : GattClientCharacteristicConfigurationDescriptorValue.Notify;

        var status = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
        if (status != GattCommunicationStatus.Success)
            throw new BleException(BleErrorCode.OperationFailed, $"Failed to enable notifications: {status}");
    }

    public async Task StopNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _characteristic.ValueChanged -= OnValueChanged;
        lock (_handlerLock) { _notificationHandler = null; }
        await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.None);
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        Action<byte[]>? handler;
        lock (_handlerLock) { handler = _notificationHandler; }
        handler?.Invoke(ReadBytes(args.CharacteristicValue));
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
